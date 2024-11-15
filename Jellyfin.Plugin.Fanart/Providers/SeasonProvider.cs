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
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Fanart.Providers
{
    public class SeasonProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public SeasonProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string Name => "Fanart";

        /// <inheritdoc />
        public int Order => 1;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
            => item is Season;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
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
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            var season = (Season)item;

            if (season != null)
            {
                var id = season.GetProviderId(MetadataProvider.Tmdb) ?? season.GetProviderId(MetadataProvider.Tvdb);

                if (!string.IsNullOrEmpty(id) && season.IndexNumber.HasValue)
                {
                    // Bad id entered
                    try
                    {
                        await SeriesProvider.Current.EnsureSeriesJson(id, cancellationToken).ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex)
                    {
                        if (!ex.StatusCode.HasValue || ex.StatusCode.Value != HttpStatusCode.NotFound)
                        {
                            throw;
                        }
                    }

                    var path = SeriesProvider.Current.GetJsonPath(id);

                    try
                    {
                        await AddImages(list, season.IndexNumber.Value, path, cancellationToken).ConfigureAwait(false);
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

        private async Task AddImages(List<RemoteImageInfo> list, int seasonNumber, string path, CancellationToken cancellationToken)
        {
            Stream fileStream = File.OpenRead(path);
            var root = await JsonSerializer.DeserializeAsync<SeriesRootObject>(fileStream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false);

            AddImages(list, root, seasonNumber, cancellationToken);
        }

        private void AddImages(List<RemoteImageInfo> list, SeriesRootObject obj, int seasonNumber, CancellationToken cancellationToken)
        {
            PopulateImages(list, obj.SeasonPosters, ImageType.Primary, 1000, 1426, seasonNumber);
            PopulateImages(list, obj.SeasonBanners, ImageType.Banner, 1000, 185, seasonNumber);
            PopulateImages(list, obj.SeasonThumbs, ImageType.Thumb, 1000, 562, seasonNumber);
            PopulateImages(list, obj.Showbackgrounds, ImageType.Backdrop, 1920, 1080, seasonNumber);
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
                var url = i.Url;
                var season = i.Season;

                if (!string.IsNullOrEmpty(url)
                    && !string.IsNullOrEmpty(season)
                    && int.TryParse(season, NumberStyles.Integer, CultureInfo.InvariantCulture, out var imageSeasonNumber)
                    && seasonNumber == imageSeasonNumber)
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
    }
}
