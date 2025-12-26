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
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Fanart.Providers;

public class SeriesProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IServerConfigurationManager _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileSystem _fileSystem;

    private readonly SemaphoreSlim _ensureSemaphore = new(1, 1);

    public SeriesProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _fileSystem = fileSystem;

        Current = this;
    }

    internal static SeriesProvider Current { get; private set; }

    /// <inheritdoc />
    public string Name => "Fanart";

    /// <inheritdoc />
    public int Order => 1;

    /// <inheritdoc />
    public bool Supports(BaseItem item)
        => item is Series;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return
        [
            ImageType.Primary,
            ImageType.Thumb,
            ImageType.Art,
            ImageType.Logo,
            ImageType.Backdrop,
            ImageType.Banner
        ];
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var list = new List<RemoteImageInfo>();
        var series = (Series)item;
        var id = series.GetProviderId(MetadataProvider.Tvdb);

        if (!string.IsNullOrEmpty(id))
        {
            // Bad id entered
            try
            {
                await EnsureSeriesJson(id, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (!ex.StatusCode.HasValue || ex.StatusCode.Value != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            var path = GetJsonPath(id);
            try
            {
                await AddImages(list, path, cancellationToken);
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
            .ThenByDescending(i => i.CommunityRating ?? 0)
            .ThenByDescending(i => i.VoteCount ?? 0);
    }

    private async Task AddImages(List<RemoteImageInfo> list, string path, CancellationToken cancellationToken)
    {
        Stream fileStream = File.OpenRead(path);
        var root = await JsonSerializer.DeserializeAsync<SeriesRootObject>(fileStream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false);

        AddImages(list, root);
    }

    private void AddImages(List<RemoteImageInfo> list, SeriesRootObject obj)
    {
        PopulateImages(list, obj.HdTvLogos, ImageType.Logo, 800, 310);
        PopulateImages(list, obj.HdClearArts, ImageType.Art, 1000, 562);
        PopulateImages(list, obj.ClearLogos, ImageType.Logo, 400, 155);
        PopulateImages(list, obj.ClearArts, ImageType.Art, 500, 281);
        PopulateImages(list, obj.Showbackgrounds, ImageType.Backdrop, 1920, 1080, true);
        PopulateImages(list, obj.SeasonThumbs, ImageType.Thumb, 500, 281);
        PopulateImages(list, obj.TvThumbs, ImageType.Thumb, 1000, 562);
        PopulateImages(list, obj.TvBanners, ImageType.Banner, 1000, 185);
        PopulateImages(list, obj.TvPosters, ImageType.Primary, 1000, 1426);
    }

    private void PopulateImages(
        List<RemoteImageInfo> list,
        List<SeriesImage> images,
        ImageType type,
        int width,
        int height,
        bool allowSeasonAll = false)
    {
        if (images == null)
        {
            return;
        }

        list.AddRange(images.Select(i =>
        {
            var url = i.Url;
            var season = i.Season;

            var isSeasonValid = string.IsNullOrEmpty(season) ||
                (allowSeasonAll && string.Equals(season, "all", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(url) && isSeasonValid)
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

                if (type == ImageType.Thumb && !(DateTime.TryParse(i.Added, out var added) && added >= Constants.WorkingThumbImageDimensions)) {
                    info.Width = 500;
                    info.Height = 281;
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
    /// Gets the series data path.
    /// </summary>
    /// <param name="appPaths">The app paths.</param>
    /// <param name="seriesId">The series id.</param>
    /// <returns>System.String.</returns>
    internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
    {
        var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

        return seriesDataPath;
    }

    /// <summary>
    /// Gets the series data path.
    /// </summary>
    /// <param name="appPaths">The app paths.</param>
    /// <returns>System.String.</returns>
    internal static string GetSeriesDataPath(IApplicationPaths appPaths)
    {
        var dataPath = Path.Combine(appPaths.CachePath, "fanart-tv");

        return dataPath;
    }

    public string GetJsonPath(string tvdbId)
    {
        var dataPath = GetSeriesDataPath(_config.ApplicationPaths, tvdbId);
        return Path.Combine(dataPath, "fanart.json");
    }

    internal async Task EnsureSeriesJson(string tvdbId, CancellationToken cancellationToken)
    {
        var path = GetJsonPath(tvdbId);

        // Only allow one thread in here at a time since every season will be calling this method, possibly concurrently
        await _ensureSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var fileInfo = _fileSystem.GetFileSystemInfo(path);

            if (fileInfo.Exists)
            {
                if ((DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 2)
                {
                    return;
                }
            }

            await DownloadSeriesJson(tvdbId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ensureSemaphore.Release();
        }
    }

    /// <summary>
    /// Downloads the series json.
    /// </summary>
    /// <param name="tvdbId">The TVDB identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task.</returns>
    internal async Task DownloadSeriesJson(string tvdbId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = string.Format(
            CultureInfo.InvariantCulture,
            Plugin.BaseUrl,
            Plugin.ApiKey,
            tvdbId,
            "tv");

        var clientKey = Plugin.Instance.Configuration.PersonalApiKey;
        if (!string.IsNullOrWhiteSpace(clientKey))
        {
            url += "&client_key=" + clientKey;
        }

        var path = GetJsonPath(tvdbId);

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
                await JsonSerializer.SerializeAsync(fileStream, new SeriesRootObject(), JsonDefaults.Options, cancellationToken).ConfigureAwait(false);

                return;
            }

            throw;
        }
    }
}
