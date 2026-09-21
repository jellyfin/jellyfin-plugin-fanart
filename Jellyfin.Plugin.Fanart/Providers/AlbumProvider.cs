using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Album image provider for Fanart.tv.
/// </summary>
public sealed class AlbumProvider : FanartProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public AlbumProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger<AlbumProvider> logger)
        : base(config, httpClientFactory, fileSystem, logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 1; // After embedded provider

    /// <inheritdoc />
    public override bool Supports(BaseItem item)
        => item is MusicAlbum;

    /// <inheritdoc />
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return
        [
            ImageType.Primary,
            ImageType.Disc
        ];
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var album = (MusicAlbum)item;
        var list = new List<RemoteImageInfo>();
        var musicBrainzAlbumArtist = album.GetProviderId(MetadataProvider.MusicBrainzAlbumArtist);

        if (string.IsNullOrEmpty(musicBrainzAlbumArtist))
        {
            return list;
        }

        await ArtistProvider.Current.EnsureArtistJson(musicBrainzAlbumArtist, cancellationToken).ConfigureAwait(false);

        var artistJsonPath = ArtistProvider.GetArtistJsonPath(Config.CommonApplicationPaths, musicBrainzAlbumArtist);
        var musicBrainzReleaseGroup = album.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);
        var musicBrainzAlbum = album.GetProviderId(MetadataProvider.MusicBrainzAlbum);

        await AddImages(list, artistJsonPath, musicBrainzAlbum, musicBrainzReleaseGroup, cancellationToken).ConfigureAwait(false);

        return SortImages(list, item.GetPreferredMetadataLanguage());
    }

    /// <summary>
    /// Adds the images.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="path">The path.</param>
    /// <param name="releaseId">The release identifier.</param>
    /// <param name="releaseGroupId">The release group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task AddImages(List<RemoteImageInfo> list, string path, string releaseId, string releaseGroupId, CancellationToken cancellationToken)
    {
        var artist = await TryLoad<ArtistResponse>(path, cancellationToken).ConfigureAwait(false);
        if (artist == null)
        {
            return;
        }

        var album = artist?.Albums?.FirstOrDefault(i => string.Equals(i.ReleaseGroupId, releaseId, StringComparison.OrdinalIgnoreCase) || string.Equals(i.ReleaseGroupId, releaseGroupId, StringComparison.OrdinalIgnoreCase));

        PopulateImages(list, album?.AlbumCovers, ImageType.Primary, 1000, 1000);
        PopulateImages(list, album?.CdArts, ImageType.Disc, 1000, 1000);
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
}
