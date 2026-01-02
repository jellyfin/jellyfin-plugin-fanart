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
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Movie image provider for Fanart.tv.
/// </summary>
public class MovieProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IServerConfigurationManager _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="MovieProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    public MovieProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _fileSystem = fileSystem;
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
        return
        [
            ImageType.Primary,
            ImageType.Thumb,
            ImageType.Art,
            ImageType.Logo,
            ImageType.Disc,
            ImageType.Banner,
            ImageType.Backdrop
        ];
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var baseItem = item;
        var list = new List<RemoteImageInfo>();

        var movieId = baseItem.GetProviderId(MetadataProvider.Tmdb) ?? baseItem.GetProviderId(MetadataProvider.Imdb);

        if (!string.IsNullOrEmpty(movieId))
        {
            // Bad id entered
            try
            {
                await EnsureMovieJson(movieId, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (!ex.StatusCode.HasValue || ex.StatusCode.Value != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            var path = GetJsonPath(movieId);

            try
            {
                await AddImages(list, path).ConfigureAwait(false);
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

    private async Task AddImages(List<RemoteImageInfo> list, string path)
    {
        Stream fileStream = File.OpenRead(path);
        var root = await JsonSerializer.DeserializeAsync<MovieRootObject>(fileStream, JsonDefaults.Options).ConfigureAwait(false);

        AddImages(list, root);
    }

    private void AddImages(List<RemoteImageInfo> list, MovieRootObject obj)
    {
        PopulateImages(list, obj.HdMovieClearArts, ImageType.Art, 1000, 562);
        PopulateImages(list, obj.HdMovieLogos, ImageType.Logo, 800, 310);
        PopulateImages(list, obj.MovieDiscImages, ImageType.Disc, 1000, 1000);
        PopulateImages(list, obj.MoviePosters, ImageType.Primary, 1000, 1426);
        PopulateImages(list, obj.MovieLogos, ImageType.Logo, 400, 155);
        PopulateImages(list, obj.MovieArts, ImageType.Art, 500, 281);
        PopulateImages(list, obj.MovieThumbs, ImageType.Thumb, 1000, 562);
        PopulateImages(list, obj.MovieBanners, ImageType.Banner, 1000, 185);
        PopulateImages(list, obj.MovieBackgrounds, ImageType.Backdrop, 1920, 1080);
    }

    private void PopulateImages(List<RemoteImageInfo> list, List<MovieImage> images, ImageType type, int width, int height)
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
                    Url = url,
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
            Plugin.BaseUrlFormat,
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
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            using var httpResponse = await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            using var response = httpResponse.Content;
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, IODefaults.FileStreamBufferSize, FileOptions.Asynchronous);
            await response.CopyToAsync(fileStream, CancellationToken.None).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode.HasValue && exception.StatusCode.Value == HttpStatusCode.NotFound)
            {
                // If the user has automatic updates enabled, save a dummy object to prevent repeated download attempts
                Stream fileStream = File.OpenWrite(path);
                await JsonSerializer.SerializeAsync(fileStream, new MovieRootObject(), JsonDefaults.Options, cancellationToken).ConfigureAwait(false);

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
}
