using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Configuration;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Series image provider for Fanart.tv.
/// </summary>
public sealed class SeriesProvider : FanartProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public SeriesProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger<SeriesProvider> logger)
        : base(config, httpClientFactory, fileSystem, logger)
    {
        Current = this;
    }

    internal static SeriesProvider Current { get; private set; }

    /// <inheritdoc />
    public override int Order => 1;

    /// <inheritdoc />
    public override bool Supports(BaseItem item)
        => item is Series;

    /// <inheritdoc />
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
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
    public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
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
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.LogDebug(ex, "Fanart.tv has no data for series {Id}", id);
            }

            var path = GetJsonPath(id);
            await AddImages(list, path, cancellationToken).ConfigureAwait(false);
        }

        return SortImages(list, item.GetPreferredMetadataLanguage());
    }

    private async Task AddImages(List<RemoteImageInfo> list, string path, CancellationToken cancellationToken)
    {
        var series = await TryLoad<SeriesRootObject>(path, cancellationToken).ConfigureAwait(false);
        if (series == null)
        {
            return;
        }

        PopulateImages(list, series.HdTvLogos, ImageType.Logo, 800, 310);
        PopulateImages(list, series.HdClearArts, ImageType.Art, 1000, 562);
        PopulateImages(list, series.ClearLogos, ImageType.Logo, 400, 155);
        PopulateImages(list, series.ClearArts, ImageType.Art, 500, 281);
        PopulateImages(list, series.Showbackgrounds, ImageType.Backdrop, 1920, 1080, true);
        PopulateImages(list, series.SeasonThumbs, ImageType.Thumb, 500, 281);
        PopulateImages(list, series.TvThumbs, ImageType.Thumb, 1000, 562);
        PopulateImages(list, series.TvBanners, ImageType.Banner, 1000, 185);
        PopulateImages(list, series.TvPosters, ImageType.Primary, 1000, 1426);
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
            var season = i.Season;

            var isSeasonValid = string.IsNullOrEmpty(season) ||
                (allowSeasonAll && string.Equals(season, "all", StringComparison.OrdinalIgnoreCase));

            if (isSeasonValid)
            {
                var info = CreateImageInfo(i.Url, i.Likes, i.Language, type, width, height);

                if (info != null && type == ImageType.Thumb && !(DateTime.TryParse(i.Added, out var added) && added >= Constants.WorkingThumbImageDimensions))
                {
                    info.Width = 500;
                    info.Height = 281;
                }

                return info;
            }

            return null;
        }).Where(i => i != null));
    }

    /// <summary>
    /// Gets the series data path.
    /// </summary>
    /// <param name="appPaths">The app paths.</param>
    /// <param name="seriesId">The series id.</param>
    /// <returns>System.String.</returns>
    internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
    {
        return Path.Combine(GetSeriesDataPath(appPaths), seriesId);
    }

    /// <summary>
    /// Gets the series data path.
    /// </summary>
    /// <param name="appPaths">The app paths.</param>
    /// <returns>System.String.</returns>
    internal static string GetSeriesDataPath(IApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.CachePath, "fanart-tv");
    }

    /// <summary>
    /// Gets the path to the cached JSON file for a series.
    /// </summary>
    /// <param name="tvdbId">The TVDB identifier.</param>
    /// <returns>The path to the JSON file.</returns>
    internal string GetJsonPath(string tvdbId)
    {
        return Path.Combine(GetSeriesDataPath(Config.ApplicationPaths, tvdbId), "fanart.json");
    }

    internal Task EnsureSeriesJson(string tvdbId, CancellationToken cancellationToken)
    {
        return EnsureJson<SeriesRootObject>(tvdbId, "tv", GetJsonPath(tvdbId), cancellationToken);
    }
}
