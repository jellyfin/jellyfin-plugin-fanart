using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Artist image provider for Fanart.tv.
/// </summary>
public sealed class ArtistProvider : FanartProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtistProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public ArtistProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger<ArtistProvider> logger)
        : base(config, httpClientFactory, fileSystem, logger)
    {
        Current = this;
    }

    internal static ArtistProvider Current { get; private set; }

    /// <inheritdoc />
    public override int Order => 0;

    /// <inheritdoc />
    public override bool Supports(BaseItem item)
        => item is MusicArtist;

    /// <inheritdoc />
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
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
    public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var artist = (MusicArtist)item;
        var list = new List<RemoteImageInfo>();
        var artistMusicBrainzId = artist.GetProviderId(MetadataProvider.MusicBrainzArtist);

        if (!string.IsNullOrEmpty(artistMusicBrainzId))
        {
            await EnsureArtistJson(artistMusicBrainzId, cancellationToken).ConfigureAwait(false);

            var artistJsonPath = GetArtistJsonPath(Config.CommonApplicationPaths, artistMusicBrainzId);

            await AddImages(list, artistJsonPath, cancellationToken).ConfigureAwait(false);
        }

        return SortImages(list, item.GetPreferredMetadataLanguage());
    }

    /// <summary>
    /// Adds the images.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task AddImages(List<RemoteImageInfo> list, string path, CancellationToken cancellationToken)
    {
        var artist = await TryLoad<ArtistResponse>(path, cancellationToken).ConfigureAwait(false);
        if (artist == null)
        {
            return;
        }

        PopulateImages(list, artist.ArtistBackgrounds, ImageType.Backdrop, 1920, 1080);
        PopulateImages(list, artist.ArtistThumbs, ImageType.Primary, 500, 281);
        PopulateImages(list, artist.HdMusicLogos, ImageType.Logo, 800, 310);
        PopulateImages(list, artist.MusicBanners, ImageType.Banner, 1000, 185);
        PopulateImages(list, artist.MusicLogos, ImageType.Logo, 400, 155);
        PopulateImages(list, artist.HdmusicArts, ImageType.Art, 1000, 562);
        PopulateImages(list, artist.MusicArts, ImageType.Art, 500, 281);
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

        list.AddRange(images
            .Select(i => CreateImageInfo(i.Url, i.Likes, i.Language, type, width, height))
            .Where(i => i != null));
    }

    internal Task EnsureArtistJson(string musicBrainzId, CancellationToken cancellationToken)
    {
        var jsonPath = GetArtistJsonPath(Config.ApplicationPaths, musicBrainzId);

        return EnsureJson<ArtistResponse>(musicBrainzId, "music", jsonPath, cancellationToken);
    }

    /// <summary>
    /// Gets the artist data path.
    /// </summary>
    /// <param name="appPaths">The application paths.</param>
    /// <param name="musicBrainzArtistId">The music brainz artist identifier.</param>
    /// <returns>System.String.</returns>
    private static string GetArtistDataPath(IApplicationPaths appPaths, string musicBrainzArtistId)
    {
        return Path.Combine(GetArtistDataPath(appPaths), musicBrainzArtistId);
    }

    /// <summary>
    /// Gets the artist data path.
    /// </summary>
    /// <param name="appPaths">The application paths.</param>
    /// <returns>System.String.</returns>
    internal static string GetArtistDataPath(IApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.CachePath, "fanart-music");
    }

    internal static string GetArtistJsonPath(IApplicationPaths appPaths, string musicBrainzArtistId)
    {
        return Path.Combine(GetArtistDataPath(appPaths, musicBrainzArtistId), "fanart.json");
    }
}
