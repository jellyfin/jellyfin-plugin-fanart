using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.Fanart.Dtos;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Fanart.Providers
{
    public class AlbumProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public AlbumProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string Name => "Fanart";

        /// <inheritdoc />
        public int Order => 1; // After embedded provider

        /// <inheritdoc />
        public bool Supports(BaseItem item)
            => item is MusicAlbum;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Disc
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var album = (MusicAlbum)item;

            var list = new List<RemoteImageInfo>();

            var musicBrainzAlbumArtist = album.GetProviderId(MetadataProvider.MusicBrainzAlbumArtist);

            if (musicBrainzAlbumArtist == null)
            {
                return list;
            }

            if (!string.IsNullOrEmpty(musicBrainzAlbumArtist))
            {
                await ArtistProvider.Current.EnsureArtistJson(musicBrainzAlbumArtist, cancellationToken).ConfigureAwait(false);

                var artistJsonPath = ArtistProvider.GetArtistJsonPath(_config.CommonApplicationPaths, musicBrainzAlbumArtist);

                var musicBrainzReleaseGroup = album.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);

                var musicBrainzAlbum = album.GetProviderId(MetadataProvider.MusicBrainzAlbum);

                try
                {
                    await AddImages(list, artistJsonPath, musicBrainzAlbum, musicBrainzReleaseGroup, cancellationToken).ConfigureAwait(false);
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
        /// <param name="releaseId">The release identifier.</param>
        /// <param name="releaseGroupId">The release group identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task AddImages(List<RemoteImageInfo> list, string path, string releaseId, string releaseGroupId, CancellationToken cancellationToken)
        {
            Stream fileStream = File.OpenRead(path);
            var obj = await JsonSerializer.DeserializeAsync<ArtistResponse>(fileStream, JsonDefaults.Options).ConfigureAwait(false);

            if (obj.Albums != null)
            {
                var album = obj.Albums.FirstOrDefault(i => string.Equals(i.ReleaseGroupId, releaseId, StringComparison.OrdinalIgnoreCase) || string.Equals(i.ReleaseGroupId, releaseGroupId, StringComparison.OrdinalIgnoreCase));
                var albumcovers = album?.AlbumCovers;
                var cdarts = album?.CdArts;

                if (albumcovers != null)
                {
                    PopulateImages(list, albumcovers, ImageType.Primary, 1000, 1000);
                }

                if (cdarts != null)
                {
                    PopulateImages(list, cdarts, ImageType.Disc, 1000, 1000);
                }
            }
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

                    var info = new RemoteImageInfo
                    {
                        RatingType = RatingType.Likes,
                        Type = type,
                        Width = width,
                        Height = height,
                        ProviderName = Name,
                        Url = url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase),
                        Language = i.Language
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
    }
}
