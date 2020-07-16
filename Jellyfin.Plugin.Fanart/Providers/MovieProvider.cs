using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Fanart.Providers
{
    public class MovieProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IJsonSerializer _json;

        public MovieProvider(IServerConfigurationManager config, IHttpClient httpClient, IFileSystem fileSystem, IJsonSerializer json)
        {
            _config = config;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _json = json;
        }

        /// <inheritdoc />
        public string Name => "Fanart";

        /// <inheritdoc />
        public int Order => 1;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
            => item is Movie || item is BoxSet || item is MusicVideo;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Thumb,
                ImageType.Art,
                ImageType.Logo,
                ImageType.Disc,
                ImageType.Banner,
                ImageType.Backdrop
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var baseItem = item;
            var list = new List<RemoteImageInfo>();

            var movieId = baseItem.GetProviderId(MetadataProvider.Tmdb);

            if (!string.IsNullOrEmpty(movieId))
            {
                // Bad id entered
                try
                {
                    await EnsureMovieJson(movieId, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpException ex)
                {
                    if (!ex.StatusCode.HasValue || ex.StatusCode.Value != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }

                var path = GetJsonPath(movieId);

                try
                {
                    AddImages(list, path);
                }
                catch (FileNotFoundException)
                {
                    // No biggie. Don't blow up
                }
                catch (IOException)
                {
                    // No biggie. Don't blow up
                }
            }

            var language = item.GetPreferredMetadataLanguage();

            var isLanguageEn = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

            // Sort first by width to prioritize HD versions
            return list.OrderByDescending(i => i.Width ?? 0)
                .ThenByDescending(i =>
                {
                    if (string.Equals(language, i.Language, StringComparison.OrdinalIgnoreCase))
                    {
                        return 3;
                    }

                    if (!isLanguageEn)
                    {
                        if (string.Equals("en", i.Language, StringComparison.OrdinalIgnoreCase))
                        {
                            return 2;
                        }
                    }

                    if (string.IsNullOrEmpty(i.Language))
                    {
                        return isLanguageEn ? 3 : 2;
                    }

                    return 0;
                })
                .ThenByDescending(i => i.CommunityRating ?? 0);
        }

        private void AddImages(List<RemoteImageInfo> list, string path)
        {
            var root = _json.DeserializeFromFile<RootObject>(path);

            AddImages(list, root);
        }

        private void AddImages(List<RemoteImageInfo> list, RootObject obj)
        {
            PopulateImages(list, obj.hdmovieclearart, ImageType.Art, 1000, 562);
            PopulateImages(list, obj.hdmovielogo, ImageType.Logo, 800, 310);
            PopulateImages(list, obj.moviedisc, ImageType.Disc, 1000, 1000);
            PopulateImages(list, obj.movieposter, ImageType.Primary, 1000, 1426);
            PopulateImages(list, obj.movielogo, ImageType.Logo, 400, 155);
            PopulateImages(list, obj.movieart, ImageType.Art, 500, 281);
            PopulateImages(list, obj.moviethumb, ImageType.Thumb, 1000, 562);
            PopulateImages(list, obj.moviebanner, ImageType.Banner, 1000, 185);
            PopulateImages(list, obj.moviebackground, ImageType.Backdrop, 1920, 1080);
        }

        private void PopulateImages(List<RemoteImageInfo> list, List<Image> images, ImageType type, int width, int height)
        {
            if (images == null)
            {
                return;
            }

            list.AddRange(images.Select(i =>
            {
                var url = i.url;

                if (!string.IsNullOrEmpty(url))
                {
                    var likesString = i.likes;

                    var info = new RemoteImageInfo
                    {
                        RatingType = RatingType.Likes,
                        Type = type,
                        Width = width,
                        Height = height,
                        ProviderName = Name,
                        Url = url,
                        Language = i.lang
                    };

                    if (!string.IsNullOrEmpty(likesString)
                        && int.TryParse(likesString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var likes))
                    {
                        info.CommunityRating = likes;
                    }

                    return info;
                }

                return null;
            }).Where(i => i != null));
        }

        /// <inheritdoc />
        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url
            });
        }

        /// <summary>
        /// Gets the movie data path.
        /// </summary>
        /// <param name="appPaths">The application paths.</param>
        /// <param name="id">The identifier.</param>
        /// <returns>System.String.</returns>
        internal static string GetMovieDataPath(IApplicationPaths appPaths, string id)
        {
            var dataPath = Path.Combine(GetMoviesDataPath(appPaths), id);

            return dataPath;
        }

        /// <summary>
        /// Gets the movie data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetMoviesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "fanart-movies");

            return dataPath;
        }

        private string GetJsonPath(string id)
        {
            var movieDataPath = GetMovieDataPath(_config.ApplicationPaths, id);
            return Path.Combine(movieDataPath, "fanart.json");
        }

        /// <summary>
        /// Downloads the movie json.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task DownloadMovieJson(string id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = string.Format(
                CultureInfo.InvariantCulture,
                Plugin.BaseUrl,
                Plugin.ApiKey,
                id,
                "movies");

            var clientKey = Plugin.Instance.Configuration.PersonalApiKey;
            if (!string.IsNullOrWhiteSpace(clientKey))
            {
                url += "&client_key=" + clientKey;
            }

            var path = GetJsonPath(id);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try
            {
                using (var httpResponse = await _httpClient.SendAsync(
                    new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        BufferContent = true

                    },
                    HttpMethod.Get).ConfigureAwait(false))
                using (var response = httpResponse.Content)
                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, IODefaults.FileStreamBufferSize, FileOptions.Asynchronous))
                {
                    await response.CopyToAsync(fileStream, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (HttpException exception)
            {
                if (exception.StatusCode.HasValue && exception.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    // If the user has automatic updates enabled, save a dummy object to prevent repeated download attempts
                    _json.SerializeToFile(new RootObject(), path);

                    return;
                }

                throw;
            }
        }

        internal Task EnsureMovieJson(string id, CancellationToken cancellationToken)
        {
            var path = GetJsonPath(id);

            var fileInfo = _fileSystem.GetFileSystemInfo(path);

            if (fileInfo.Exists
                && (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 2)
            {
                return Task.CompletedTask;
            }

            return DownloadMovieJson(id, cancellationToken);
        }

        public class Image
        {
            public string id { get; set; }

            public string url { get; set; }

            public string lang { get; set; }

            public string likes { get; set; }
        }

        public class RootObject
        {
            public string name { get; set; }

            public string tmdb_id { get; set; }

            public string imdb_id { get; set; }

            public List<Image> hdmovielogo { get; set; }

            public List<Image> moviedisc { get; set; }

            public List<Image> movielogo { get; set; }

            public List<Image> movieposter { get; set; }

            public List<Image> hdmovieclearart { get; set; }

            public List<Image> movieart { get; set; }

            public List<Image> moviebackground { get; set; }

            public List<Image> moviebanner { get; set; }

            public List<Image> moviethumb { get; set; }
        }
    }
}
