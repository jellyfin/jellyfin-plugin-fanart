using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Configuration;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Season image provider for Fanart.tv.
/// </summary>
public sealed class SeasonProvider : FanartProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public SeasonProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger<SeasonProvider> logger)
        : base(config, httpClientFactory, fileSystem, logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 1;

    /// <inheritdoc />
    public override bool Supports(BaseItem item)
        => item is Season;

    /// <inheritdoc />
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return
        [
            ImageType.Backdrop,
            ImageType.Thumb,
            ImageType.Banner,
            ImageType.Primary
        ];
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var list = new List<RemoteImageInfo>();

        var season = (Season)item;
        var id = season.GetProviderId(MetadataProvider.Tmdb) ?? season.GetProviderId(MetadataProvider.Tvdb);

        if (!string.IsNullOrEmpty(id) && season.IndexNumber.HasValue)
        {
            // Bad id entered
            try
            {
                await SeriesProvider.Current.EnsureSeriesJson(id, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.LogDebug(ex, "Fanart.tv has no data for season {Id}", id);
            }

            var path = SeriesProvider.Current.GetJsonPath(id);

            await AddImages(list, season.IndexNumber.Value, path, cancellationToken).ConfigureAwait(false);
        }

        return SortImages(list, item.GetPreferredMetadataLanguage());
    }

    private async Task AddImages(List<RemoteImageInfo> list, int seasonNumber, string path, CancellationToken cancellationToken)
    {
        var root = await TryLoad<SeriesRootObject>(path, cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        PopulateImages(list, root.SeasonPosters, ImageType.Primary, 1000, 1426, seasonNumber);
        PopulateImages(list, root.SeasonBanners, ImageType.Banner, 1000, 185, seasonNumber);
        PopulateImages(list, root.SeasonThumbs, ImageType.Thumb, 1000, 562, seasonNumber);
        PopulateImages(list, root.Showbackgrounds, ImageType.Backdrop, 1920, 1080, seasonNumber);
    }

    private void PopulateImages(
        List<RemoteImageInfo> list,
        List<SeriesImage> images,
        ImageType type,
        int width,
        int height,
        int seasonNumber)
    {
        if (images == null)
        {
            return;
        }

        list.AddRange(images.Select(i =>
        {
            var season = i.Season;

            if (!string.IsNullOrEmpty(season)
                && int.TryParse(season, NumberStyles.Integer, CultureInfo.InvariantCulture, out var imageSeasonNumber)
                && seasonNumber == imageSeasonNumber)
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
}
