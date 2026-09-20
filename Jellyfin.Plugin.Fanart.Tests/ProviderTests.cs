using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Fanart.Tests;

[Collection(PluginStatics.Name)]
public sealed class ProviderTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public void Supports_MatchesItemTypes()
    {
        var movie = new MovieProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<MovieProvider>.Instance);
        var series = new SeriesProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<SeriesProvider>.Instance);
        var season = new SeasonProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<SeasonProvider>.Instance);
        var artist = new ArtistProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<ArtistProvider>.Instance);
        var album = new AlbumProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<AlbumProvider>.Instance);

        Assert.True(movie.Supports(new Movie()));
        Assert.True(movie.Supports(new BoxSet()));
        Assert.True(movie.Supports(new MusicVideo()));
        Assert.False(movie.Supports(new Series()));

        Assert.True(series.Supports(new Series()));
        Assert.False(series.Supports(new Season()));

        Assert.True(season.Supports(new Season()));
        Assert.False(season.Supports(new Series()));

        Assert.True(artist.Supports(new MusicArtist()));
        Assert.False(artist.Supports(new MusicAlbum()));

        Assert.True(album.Supports(new MusicAlbum()));
        Assert.False(album.Supports(new MusicArtist()));

        movie.Dispose();
        series.Dispose();
        season.Dispose();
        artist.Dispose();
        album.Dispose();
    }

    [Fact]
    public void Order_ArtistFirst()
    {
        using var artist = new ArtistProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<ArtistProvider>.Instance);
        using var album = new AlbumProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<AlbumProvider>.Instance);

        Assert.True(artist.Order < album.Order);
    }

    [Fact]
    public async Task Movie_NoProviderId_ReturnsEmptyWithoutDownload()
    {
        using var provider = NewMovieProvider();

        var images = await provider.GetImages(new Movie { PreferredMetadataLanguage = "en" }, TestContext.Current.CancellationToken);

        Assert.Empty(images);
        Assert.Equal(0, _env.Handler.CallCount);
    }

    [Fact]
    public async Task Movie_MapsImageTypesAndSizes()
    {
        SeedMovie("603", """
            {
              "hdmovielogo": [{"url":"https://a/fanart/hdlogo.png","lang":"en","likes":"5"}],
              "movielogo": [{"url":"https://a/fanart/logo.png","lang":"en","likes":"1"}],
              "movieposter": [{"url":"https://a/fanart/poster.jpg","lang":"00","likes":"2"}],
              "moviebackground": [{"url":"https://a/fanart/bg.jpg","likes":"3"}],
              "moviethumb": [{"url":"https://a/fanart/thumb.jpg","lang":"en","likes":"3"}],
              "moviebanner": [{"url":"","lang":"en","likes":"3"}]
            }
            """);
        using var provider = NewMovieProvider();
        var movie = new Movie { PreferredMetadataLanguage = "en" };
        movie.SetProviderId(MetadataProvider.Tmdb, "603");

        var images = (await provider.GetImages(movie, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(5, images.Count);
        Assert.Contains(images, i => i.Type == ImageType.Logo && i.Width == 800 && i.Height == 310);
        Assert.Contains(images, i => i.Type == ImageType.Logo && i.Width == 400 && i.Height == 155);
        Assert.Contains(images, i => i.Type == ImageType.Primary && i.Width == 1000 && i.Height == 1426 && i.Language == null);
        Assert.Contains(images, i => i.Type == ImageType.Backdrop && i.Width == 1920 && i.Height == 1080);
        Assert.Contains(images, i => i.Type == ImageType.Thumb);
        Assert.DoesNotContain(images, i => i.Type == ImageType.Banner);
        Assert.Equal(0, _env.Handler.CallCount);

        // Widest image first.
        Assert.Equal(1920, images[0].Width);
    }

    [Fact]
    public async Task Movie_FallsBackToImdbId()
    {
        SeedMovie("tt0133093", """{"movieposter":[{"url":"https://a/fanart/p.jpg","lang":"en","likes":"1"}]}""");
        using var provider = NewMovieProvider();
        var movie = new Movie { PreferredMetadataLanguage = "en" };
        movie.SetProviderId(MetadataProvider.Imdb, "tt0133093");

        var images = await provider.GetImages(movie, TestContext.Current.CancellationToken);

        Assert.Single(images);
    }

    [Fact]
    public async Task Movie_UnknownId_NotFoundIsSwallowedAndReturnsEmpty()
    {
        using var env = new TestEnvironment(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var provider = new MovieProvider(env.Config, env.HttpClientFactory, env.FileSystem, NullLogger<MovieProvider>.Instance);
        var movie = new Movie { PreferredMetadataLanguage = "en" };
        movie.SetProviderId(MetadataProvider.Tmdb, "999");

        var images = await provider.GetImages(movie, TestContext.Current.CancellationToken);

        Assert.Empty(images);
        Assert.Equal(1, env.Handler.CallCount);
    }

    [Fact]
    public async Task Movie_ServerError_Propagates()
    {
        using var env = new TestEnvironment(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var provider = new MovieProvider(env.Config, env.HttpClientFactory, env.FileSystem, NullLogger<MovieProvider>.Instance);
        var movie = new Movie { PreferredMetadataLanguage = "en" };
        movie.SetProviderId(MetadataProvider.Tmdb, "999");

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetImages(movie, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Series_FiltersSeasonSpecificAndAllowsSeasonAllBackgrounds()
    {
        SeedSeries("121361", """
            {
              "clearlogo": [{"url":"https://a/fanart/logo.png","lang":"en","likes":"1"}],
              "showbackground": [
                {"url":"https://a/fanart/bg-all.jpg","season":"all","likes":"1"},
                {"url":"https://a/fanart/bg-none.jpg","likes":"1"},
                {"url":"https://a/fanart/bg-s1.jpg","season":"1","likes":"1"}
              ],
              "tvposter": [{"url":"https://a/fanart/p-s2.jpg","season":"2","likes":"1"}],
              "tvthumb": [
                {"url":"https://a/fanart/old.jpg","added":"2015-01-01","likes":"1"},
                {"url":"https://a/fanart/new.jpg","added":"2020-01-01","likes":"1"}
              ]
            }
            """);
        using var provider = NewSeriesProvider();
        var series = new Series { PreferredMetadataLanguage = "en" };
        series.SetProviderId(MetadataProvider.Tvdb, "121361");

        var images = (await provider.GetImages(series, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(new[] { "bg-all.jpg", "bg-none.jpg" }, images.Where(i => i.Type == ImageType.Backdrop).Select(i => Leaf(i.Url)).OrderBy(x => x, StringComparer.Ordinal));
        Assert.DoesNotContain(images, i => i.Type == ImageType.Primary);

        var thumbs = images.Where(i => i.Type == ImageType.Thumb).ToDictionary(i => Leaf(i.Url));
        Assert.Equal((500, 281), (thumbs["old.jpg"].Width, thumbs["old.jpg"].Height));
        Assert.Equal((1000, 562), (thumbs["new.jpg"].Width, thumbs["new.jpg"].Height));
    }

    [Fact]
    public async Task Series_NoTvdbId_ReturnsEmpty()
    {
        using var provider = NewSeriesProvider();

        var images = await provider.GetImages(new Series { PreferredMetadataLanguage = "en" }, TestContext.Current.CancellationToken);

        Assert.Empty(images);
        Assert.Equal(0, _env.Handler.CallCount);
    }

    [Fact]
    public async Task Season_ReturnsOnlyMatchingSeasonImages()
    {
        SeedSeries("121361", """
            {
              "seasonposter": [
                {"url":"https://a/fanart/s1.jpg","season":"1","likes":"1"},
                {"url":"https://a/fanart/s2.jpg","season":"2","likes":"1"},
                {"url":"https://a/fanart/none.jpg","likes":"1"}
              ],
              "seasonbanner": [{"url":"https://a/fanart/b1.jpg","season":"1","likes":"1"}]
            }
            """);
        using var series = NewSeriesProvider();
        using var provider = new SeasonProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<SeasonProvider>.Instance);
        var season = new Season { IndexNumber = 1, PreferredMetadataLanguage = "en" };
        season.SetProviderId(MetadataProvider.Tvdb, "121361");

        var images = (await provider.GetImages(season, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(new[] { "b1.jpg", "s1.jpg" }, images.Select(i => Leaf(i.Url)).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Season_WithoutIndexNumber_ReturnsEmpty()
    {
        using var series = NewSeriesProvider();
        using var provider = new SeasonProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<SeasonProvider>.Instance);
        var season = new Season { PreferredMetadataLanguage = "en" };
        season.SetProviderId(MetadataProvider.Tvdb, "121361");

        var images = await provider.GetImages(season, TestContext.Current.CancellationToken);

        Assert.Empty(images);
        Assert.Equal(0, _env.Handler.CallCount);
    }

    [Fact]
    public async Task Artist_MapsImageTypes()
    {
        SeedArtist("11111111-1111-1111-1111-111111111111", """
            {
              "artistbackground": [{"url":"https://a/fanart/bg.jpg","likes":"1"}],
              "artistthumb": [{"url":"https://a/fanart/thumb.jpg","likes":"1"}],
              "hdmusiclogo": [{"url":"https://a/fanart/hd.png","likes":"1"}],
              "musicbanner": [{"url":"https://a/fanart/ban.jpg","likes":"1"}],
              "hdmusicarts": [{"url":"https://a/fanart/art.png","likes":"1"}]
            }
            """);
        using var provider = NewArtistProvider();
        var artist = new MusicArtist { PreferredMetadataLanguage = "en" };
        artist.SetProviderId(MetadataProvider.MusicBrainzArtist, "11111111-1111-1111-1111-111111111111");

        var images = (await provider.GetImages(artist, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(
            new[] { ImageType.Art, ImageType.Backdrop, ImageType.Banner, ImageType.Logo, ImageType.Primary }.OrderBy(t => t),
            images.Select(i => i.Type).OrderBy(t => t));
    }

    [Fact]
    public async Task Artist_NoMusicBrainzId_ReturnsEmpty()
    {
        using var provider = NewArtistProvider();

        var images = await provider.GetImages(new MusicArtist { PreferredMetadataLanguage = "en" }, TestContext.Current.CancellationToken);

        Assert.Empty(images);
        Assert.Equal(0, _env.Handler.CallCount);
    }

    [Fact]
    public async Task Album_MatchesReleaseGroupAndReturnsCoverAndDisc()
    {
        SeedArtist("22222222-2222-2222-2222-222222222222", """
            {
              "albums": [
                {"release_group_id":"other","albumcover":[{"url":"https://a/fanart/no.jpg","likes":"1"}]},
                {"release_group_id":"33333333-3333-3333-3333-333333333333","albumcover":[{"url":"https://a/fanart/cover.jpg","likes":"1"}],"cdart":[{"url":"https://a/fanart/disc.png","likes":"1"}]}
              ]
            }
            """);
        using var artist = NewArtistProvider();
        using var provider = new AlbumProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<AlbumProvider>.Instance);
        var album = new MusicAlbum { PreferredMetadataLanguage = "en" };
        album.SetProviderId(MetadataProvider.MusicBrainzAlbumArtist, "22222222-2222-2222-2222-222222222222");
        album.SetProviderId(MetadataProvider.MusicBrainzReleaseGroup, "33333333-3333-3333-3333-333333333333");

        var images = (await provider.GetImages(album, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(2, images.Count);
        Assert.Contains(images, i => i.Type == ImageType.Primary);
        Assert.Contains(images, i => i.Type == ImageType.Disc);
    }

    [Fact]
    public async Task Album_NoAlbumArtistId_ReturnsEmpty()
    {
        using var provider = new AlbumProvider(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<AlbumProvider>.Instance);

        var images = await provider.GetImages(new MusicAlbum { PreferredMetadataLanguage = "en" }, TestContext.Current.CancellationToken);

        Assert.Empty(images);
    }

    private static string Leaf(string url) => url[(url.LastIndexOf('/') + 1)..];

    private MovieProvider NewMovieProvider()
        => new(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<MovieProvider>.Instance);

    private SeriesProvider NewSeriesProvider()
        => new(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<SeriesProvider>.Instance);

    private ArtistProvider NewArtistProvider()
        => new(_env.Config, _env.HttpClientFactory, _env.FileSystem, NullLogger<ArtistProvider>.Instance);

    private void SeedMovie(string id, string json) => _env.WriteCacheFile($"fanart-movies/{id}/fanart.json", json);

    private void SeedSeries(string id, string json) => _env.WriteCacheFile($"fanart-tv/{id}/fanart.json", json);

    private void SeedArtist(string id, string json) => _env.WriteCacheFile($"fanart-music/{id}/fanart.json", json);
}
