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
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Movie image provider for Fanart.tv.
/// </summary>
public sealed class MovieProvider : FanartProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MovieProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public MovieProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger<MovieProvider> logger)
        : base(config, httpClientFactory, fileSystem, logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 1;

    /// <inheritdoc />
    public override bool Supports(BaseItem item)
        => item is Movie || item is BoxSet || item is MusicVideo;

    /// <inheritdoc />
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
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
    public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var list = new List<RemoteImageInfo>();

        var movieId = item.GetProviderId(MetadataProvider.Tmdb) ?? item.GetProviderId(MetadataProvider.Imdb);

        if (!string.IsNullOrEmpty(movieId))
        {
            await EnsureMovieJson(movieId, cancellationToken).ConfigureAwait(false);

            var path = GetJsonPath(movieId);
            await AddImages(list, path, cancellationToken).ConfigureAwait(false);
        }

        return SortImages(list, item.GetPreferredMetadataLanguage());
    }

    private async Task AddImages(List<RemoteImageInfo> list, string path, CancellationToken cancellationToken)
    {
        var movie = await TryLoad<MovieRootObject>(path, cancellationToken).ConfigureAwait(false);
        if (movie == null)
        {
            return;
        }

        PopulateImages(list, movie.HdMovieClearArts, ImageType.Art, 1000, 562);
        PopulateImages(list, movie.HdMovieLogos, ImageType.Logo, 800, 310);
        PopulateImages(list, movie.MovieDiscImages, ImageType.Disc, 1000, 1000);
        PopulateImages(list, movie.MoviePosters, ImageType.Primary, 1000, 1426);
        PopulateImages(list, movie.MovieLogos, ImageType.Logo, 400, 155);
        PopulateImages(list, movie.MovieArts, ImageType.Art, 500, 281);
        PopulateImages(list, movie.MovieThumbs, ImageType.Thumb, 1000, 562);
        PopulateImages(list, movie.MovieBanners, ImageType.Banner, 1000, 185);
        PopulateImages(list, movie.MovieBackgrounds, ImageType.Backdrop, 1920, 1080);
    }

    private void PopulateImages(List<RemoteImageInfo> list, List<MovieImage> images, ImageType type, int width, int height)
    {
        if (images == null)
        {
            return;
        }

        list.AddRange(images
            .Select(i => CreateImageInfo(i.Url, i.Likes, i.Language, type, width, height))
            .Where(i => i != null));
    }

    /// <summary>
    /// Gets the movie data path.
    /// </summary>
    /// <param name="appPaths">The application paths.</param>
    /// <param name="id">The identifier.</param>
    /// <returns>System.String.</returns>
    internal static string GetMovieDataPath(IApplicationPaths appPaths, string id)
    {
        return Path.Combine(GetMoviesDataPath(appPaths), id);
    }

    /// <summary>
    /// Gets the movie data path.
    /// </summary>
    /// <param name="appPaths">The app paths.</param>
    /// <returns>System.String.</returns>
    internal static string GetMoviesDataPath(IApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.CachePath, "fanart-movies");
    }

    private string GetJsonPath(string id)
    {
        return Path.Combine(GetMovieDataPath(Config.ApplicationPaths, id), "fanart.json");
    }

    internal Task EnsureMovieJson(string id, CancellationToken cancellationToken)
    {
        return EnsureJson<MovieRootObject>(id, "movies", GetJsonPath(id), cancellationToken);
    }
}
