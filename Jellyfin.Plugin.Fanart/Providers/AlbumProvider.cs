using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Fanart.Providers
{
    public class AlbumProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;

        public AlbumProvider(IServerConfigurationManager config, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _config = config;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
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

            var musicArtist = album.MusicArtist;

            if (musicArtist == null)
            {
                return list;
            }

            var artistMusicBrainzId = musicArtist.GetProviderId(MetadataProvider.MusicBrainzArtist);

            if (!string.IsNullOrEmpty(artistMusicBrainzId))
            {
                await ArtistProvider.Current.EnsureArtistJson(artistMusicBrainzId, cancellationToken).ConfigureAwait(false);

                var artistJsonPath = ArtistProvider.GetArtistJsonPath(_config.CommonApplicationPaths, artistMusicBrainzId);

                var musicBrainzReleaseGroupId = album.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);

                var musicBrainzId = album.GetProviderId(MetadataProvider.MusicBrainzAlbum);

                try
                {
                    AddImages(list, artistJsonPath, musicBrainzId, musicBrainzReleaseGroupId, cancellationToken);
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
        private void AddImages(List<RemoteImageInfo> list, string path, string releaseId, string releaseGroupId, CancellationToken cancellationToken)
        {
            var obj = _jsonSerializer.DeserializeFromFile<ArtistProvider.ArtistResponse>(path);

            if (obj.albums != null)
            {
                var album = obj.albums.FirstOrDefault(i => string.Equals(i.release_group_id, releaseGroupId, StringComparison.OrdinalIgnoreCase));

                if (album != null)
                {
                    PopulateImages(list, album.albumcover, ImageType.Primary, 1000, 1000);
                    PopulateImages(list, album.cdart, ImageType.Disc, 1000, 1000);
                }
            }
        }

        private void PopulateImages(
            List<RemoteImageInfo> list,
            List<ArtistProvider.ArtistImage> images,
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
                        Url = url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase),
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
    }
}
