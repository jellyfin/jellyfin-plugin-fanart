using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.Fanart.Configuration;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Fanart.Providers
{
    public class ArtistProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IFileSystem _fileSystem;

        public ArtistProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _fileSystem = fileSystem;

            Current = this;
        }

        internal static ArtistProvider Current { get; private set; }

        /// <inheritdoc />
        public string Name => "Fanart";

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
            => item is MusicArtist;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return
            [
                ImageType.Primary,
                ImageType.Logo,
                ImageType.Art,
                ImageType.Banner,
                ImageType.Backdrop
            ];
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var artist = (MusicArtist)item;

            var list = new List<RemoteImageInfo>();

            var artistMusicBrainzId = artist.GetProviderId(MetadataProvider.MusicBrainzArtist);

            if (!string.IsNullOrEmpty(artistMusicBrainzId))
            {
                await EnsureArtistJson(artistMusicBrainzId, cancellationToken).ConfigureAwait(false);

                var artistJsonPath = GetArtistJsonPath(_config.CommonApplicationPaths, artistMusicBrainzId);

                try
                {
                    await AddImages(list, artistJsonPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {

                }
                catch (IOException)
                {

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
                .ThenByDescending(i => i.CommunityRating ?? 0)
                .ThenByDescending(i => i.VoteCount ?? 0);
        }

        /// <summary>
        /// Adds the images.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="path">The path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task AddImages(List<RemoteImageInfo> list, string path, CancellationToken cancellationToken)
        {
            Stream fileStream = File.OpenRead(path);
            var obj = await JsonSerializer.DeserializeAsync<ArtistResponse>(fileStream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false);

            PopulateImages(list, obj.ArtistBackgrounds, ImageType.Backdrop, 1920, 1080);
            PopulateImages(list, obj.ArtistThumbs, ImageType.Primary, 500, 281);
            PopulateImages(list, obj.HdMusicLogos, ImageType.Logo, 800, 310);
            PopulateImages(list, obj.MusicBanners, ImageType.Banner, 1000, 185);
            PopulateImages(list, obj.MusicLogos, ImageType.Logo, 400, 155);
            PopulateImages(list, obj.HdmusicArts, ImageType.Art, 1000, 562);
            PopulateImages(list, obj.MusicArts, ImageType.Art, 500, 281);
        }

        private void PopulateImages(
            List<RemoteImageInfo> list,
            List<ArtistImage> images,
            ImageType type,
            int width,
            int height)
        {
            if (images == null)
            {
                return;
            }

            list.AddRange(images.Select(i =>
            {
                var url = i.Url;
                if (!string.IsNullOrEmpty(url))
                {
                    var likesString = i.Likes;
                    /* Disabled until returned values are reliable
                    if (DateTime.TryParse(i.Added, out var added) && added > Constants.WorkingThumbImageDimensions)
                    {
                        if (int.TryParse(i.Width, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWidth))
                        {
                            width = parsedWidth;
                        }

                        if (int.TryParse(i.Width, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeight))
                        {
                            height = parsedWidth;
                        }
                    }
                    */


                    // Fanart sometimes uses 00 to denote images without language, Jellyfin expects null or an empty string
                    var language = i.Language;
                    if (string.Equals(language, "00", StringComparison.OrdinalIgnoreCase))
                    {
                        language = null;
                    }

                    var info = new RemoteImageInfo
                    {
                        RatingType = RatingType.Likes,
                        Type = type,
                        Width = width,
                        Height = height,
                        ProviderName = Name,
                        Url = url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase),
                        Language = language
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
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }

        internal Task EnsureArtistJson(string musicBrainzId, CancellationToken cancellationToken)
        {
            var jsonPath = GetArtistJsonPath(_config.ApplicationPaths, musicBrainzId);

            var fileInfo = _fileSystem.GetFileSystemInfo(jsonPath);

            if (fileInfo.Exists
                && (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 2)
            {
                return Task.CompletedTask;
            }

            return DownloadArtistJson(musicBrainzId, cancellationToken);
        }

        /// <summary>
        /// Downloads the artist data.
        /// </summary>
        /// <param name="musicBrainzId">The music brainz id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        internal async Task DownloadArtistJson(string musicBrainzId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = string.Format(
                CultureInfo.InvariantCulture,
                Plugin.BaseUrl,
                Plugin.ApiKey,
                musicBrainzId,
                "music");

            var clientKey = Plugin.Instance.Configuration.PersonalApiKey;
            if (!string.IsNullOrWhiteSpace(clientKey))
            {
                url += "&client_key=" + clientKey;
            }

            var jsonPath = GetArtistJsonPath(_config.ApplicationPaths, musicBrainzId);

            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            try
            {
                var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
                using var httpResponse = await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                using var response = httpResponse.Content;
                using var saveFileStream = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.Read, IODefaults.FileStreamBufferSize, FileOptions.Asynchronous);
                await response.CopyToAsync(saveFileStream, CancellationToken.None).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    Stream fileStream = File.OpenWrite(jsonPath);
                    await JsonSerializer.SerializeAsync(fileStream, new ArtistResponse(), JsonDefaults.Options, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the artist data path.
        /// </summary>
        /// <param name="appPaths">The application paths.</param>
        /// <param name="musicBrainzArtistId">The music brainz artist identifier.</param>
        /// <returns>System.String.</returns>
        private static string GetArtistDataPath(IApplicationPaths appPaths, string musicBrainzArtistId)
        {
            var dataPath = Path.Combine(GetArtistDataPath(appPaths), musicBrainzArtistId);

            return dataPath;
        }

        /// <summary>
        /// Gets the artist data path.
        /// </summary>
        /// <param name="appPaths">The application paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetArtistDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "fanart-music");

            return dataPath;
        }

        internal static string GetArtistJsonPath(IApplicationPaths appPaths, string musicBrainzArtistId)
        {
            var dataPath = GetArtistDataPath(appPaths, musicBrainzArtistId);

            return Path.Combine(dataPath, "fanart.json");
        }
    }
}
