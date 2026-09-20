using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Dtos;
using Jellyfin.Plugin.Fanart.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Fanart.Tests;

[Collection(PluginStatics.Name)]
public sealed class FanartProviderTests : IDisposable
{
    private readonly TestEnvironment _env;

    public FanartProviderTests()
    {
        _env = new TestEnvironment(_ => TestEnvironment.Json("{}"));
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public void Name_IsPluginName()
    {
        using var provider = CreateProvider();

        Assert.Equal("Fanart", provider.Name);
    }

    [Fact]
    public void SortImages_OrdersByWidthFirst()
    {
        var images = new[]
        {
            Image(width: 500, language: "en", rating: 100),
            Image(width: 1000, language: "de", rating: 1),
        };

        var sorted = TestProvider.Sort(images, "en").ToList();

        Assert.Equal(new int?[] { 1000, 500 }, sorted.Select(i => i.Width));
    }

    [Fact]
    public void SortImages_PrefersPreferredLanguageThenEnglishOrNoLanguage()
    {
        var images = new[]
        {
            Image(language: "fr"),
            Image(language: null, rating: 1),
            Image(language: "en", rating: 2),
            Image(language: "de"),
        };

        var sorted = TestProvider.Sort(images, "de").Select(i => i.Language).ToList();

        // English and no language share a rank, so rating orders them.
        Assert.Equal(new[] { "de", "en", null, "fr" }, sorted);
    }

    [Fact]
    public void SortImages_EnglishPreferred_TreatsNoLanguageAsEnglish()
    {
        var images = new[]
        {
            Image(language: "fr", rating: 9),
            Image(language: null, rating: 1),
            Image(language: "en", rating: 2),
        };

        var sorted = TestProvider.Sort(images, "EN").ToList();

        // "en" and no language tie on language rank, so rating decides. "fr" ranks last.
        Assert.Equal(new[] { "en", null, "fr" }, sorted.Select(i => i.Language));
    }

    [Fact]
    public void SortImages_TiesBreakByRatingThenVoteCount()
    {
        var images = new[]
        {
            Image(language: "en", rating: 1, votes: 50),
            Image(language: "en", rating: 5, votes: 1),
            Image(language: "en", rating: 5, votes: 10),
        };

        var sorted = TestProvider.Sort(images, "en").ToList();

        Assert.Equal(new double?[] { 5, 5, 1 }, sorted.Select(i => i.CommunityRating));
        Assert.Equal(new int?[] { 10, 1, 50 }, sorted.Select(i => i.VoteCount));
    }

    [Fact]
    public void SortImages_NullPreferredLanguage_DoesNotThrow()
    {
        var sorted = TestProvider.Sort(new[] { Image(language: "en"), Image(language: null) }, null).ToList();

        Assert.Equal(2, sorted.Count);
    }

    [Fact]
    public void CreateImageInfo_EmptyUrl_ReturnsNull()
    {
        Assert.Null(TestProvider.Create(null, "1", "en", ImageType.Logo, 1, 1));
        Assert.Null(TestProvider.Create(string.Empty, "1", "en", ImageType.Logo, 1, 1));
    }

    [Fact]
    public void CreateImageInfo_MapsFields()
    {
        var info = TestProvider.Create("https://assets.fanart.tv/fanart/movies/1/a.jpg", "42", "en", ImageType.Art, 1000, 562);

        Assert.Equal("Fanart", info.ProviderName);
        Assert.Equal(RatingType.Likes, info.RatingType);
        Assert.Equal(ImageType.Art, info.Type);
        Assert.Equal(1000, info.Width);
        Assert.Equal(562, info.Height);
        Assert.Equal("en", info.Language);
        Assert.Equal(42, info.CommunityRating);
        Assert.Equal("https://assets.fanart.tv/fanart/movies/1/a.jpg", info.Url);
        Assert.Equal("https://assets.fanart.tv/bigpreview/movies/1/a.jpg", info.ThumbnailUrl);
    }

    [Theory]
    [InlineData("00")]
    public void CreateImageInfo_LanguageZeroZero_BecomesNull(string language)
    {
        var info = TestProvider.Create("https://x/fanart/a.jpg", "1", language, ImageType.Logo, 1, 1);

        Assert.Null(info.Language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void CreateImageInfo_UnparsableLikes_LeavesRatingNull(string likes)
    {
        var info = TestProvider.Create("https://x/fanart/a.jpg", likes, "en", ImageType.Logo, 1, 1);

        Assert.Null(info.CommunityRating);
    }

    [Fact]
    public async Task GetImageResponse_UsesDefaultClient()
    {
        using var provider = CreateProvider();

        using var response = await provider.GetImageResponse("https://assets.fanart.tv/a.jpg", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://assets.fanart.tv/a.jpg", _env.Handler.LastUri.ToString());
    }

    [Fact]
    public async Task TryLoad_ValidJson_ReturnsObject()
    {
        var path = _env.WriteCacheFile("m/fanart.json", """{"hdmovielogo":[{"url":"u","lang":"en","likes":"3"}]}""");
        using var provider = CreateProvider();

        var result = await provider.Load<MovieRootObject>(path);

        Assert.NotNull(result);
        Assert.Single(result.HdMovieLogos);
        Assert.Equal("u", result.HdMovieLogos[0].Url);
    }

    [Fact]
    public async Task TryLoad_MissingFile_ReturnsNull()
    {
        using var provider = CreateProvider();

        var result = await provider.Load<MovieRootObject>(Path.Combine(_env.Root, "nope.json"));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryLoad_CorruptJson_ReturnsNullAndDeletesFile()
    {
        var path = _env.WriteCacheFile("m/fanart.json", "{ not json");
        using var provider = CreateProvider();

        var result = await provider.Load<MovieRootObject>(path);

        Assert.Null(result);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task TryLoad_FileLocked_ReturnsNullAndKeepsFile()
    {
        var path = _env.WriteCacheFile("m/fanart.json", "{}");
        using var provider = CreateProvider();

        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await provider.Load<MovieRootObject>(path);

        Assert.Null(result);
        await exclusive.DisposeAsync();
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task EnsureJson_FreshFile_DoesNotDownload()
    {
        var path = _env.WriteCacheFile("m/fanart.json", "{}", DateTime.UtcNow.AddHours(-1));
        using var provider = CreateProvider();

        await provider.Ensure<MovieRootObject>("1", "movies", path);

        Assert.Equal(0, _env.Handler.CallCount);
    }

    [Fact]
    public async Task EnsureJson_MissingFile_DownloadsAndWritesBody()
    {
        using var env = new TestEnvironment(_ => TestEnvironment.Json("""{"moviebanner":[]}"""));
        var path = Path.Combine(env.Paths.CachePath, "m", "fanart.json");
        using var provider = new TestProvider(env);

        await provider.Ensure<MovieRootObject>("tt1", "movies", path);

        Assert.Equal("""{"moviebanner":[]}""", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(1, env.Handler.CallCount);
        Assert.Contains("/movies/tt1?", env.Handler.LastUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("api_key=" + Plugin.ApiKey, env.Handler.LastUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("client_key", env.Handler.LastUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureJson_StaleFile_Redownloads()
    {
        using var env = new TestEnvironment(_ => TestEnvironment.Json("""{"new":true}"""));
        var path = env.WriteCacheFile("m/fanart.json", """{"old":true}""", DateTime.UtcNow.AddDays(-3));
        using var provider = new TestProvider(env);

        await provider.Ensure<MovieRootObject>("1", "movies", path);

        Assert.Equal("""{"new":true}""", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureJson_PersonalApiKey_AppendsEscapedClientKey()
    {
        using var env = new TestEnvironment(_ => TestEnvironment.Json("{}"));
        env.Plugin.Configuration.PersonalApiKey = "a b&c";
        var path = Path.Combine(env.Paths.CachePath, "m", "fanart.json");
        using var provider = new TestProvider(env);

        await provider.Ensure<MovieRootObject>("1", "movies", path);

        Assert.EndsWith("&client_key=a%20b%26c", env.Handler.LastUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureJson_NotFound_CachesEmptyObject()
    {
        using var env = new TestEnvironment(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var path = Path.Combine(env.Paths.CachePath, "m", "fanart.json");
        using var provider = new TestProvider(env);

        await provider.Ensure<MovieRootObject>("1", "movies", path);

        Assert.True(File.Exists(path));
        var loaded = await provider.Load<MovieRootObject>(path);
        Assert.NotNull(loaded);
        Assert.Null(loaded.HdMovieLogos);
    }

    [Fact]
    public async Task EnsureJson_ServerError_Throws()
    {
        using var env = new TestEnvironment(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var path = Path.Combine(env.Paths.CachePath, "m", "fanart.json");
        using var provider = new TestProvider(env);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => provider.Ensure<MovieRootObject>("1", "movies", path));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task EnsureJson_Cancelled_Throws()
    {
        using var provider = CreateProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.Ensure<MovieRootObject>("1", "movies", Path.Combine(_env.Root, "x", "fanart.json"), cts.Token));
    }

    [Fact]
    public async Task EnsureJson_StaleFileLocked_KeepsUsingCachedFile()
    {
        var path = _env.WriteCacheFile("m/fanart.json", "{}", DateTime.UtcNow.AddDays(-3));
        using var provider = CreateProvider();

        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await provider.Ensure<MovieRootObject>("1", "movies", path);
        await exclusive.DisposeAsync();

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task EnsureJson_ConcurrentCallers_DownloadOnce()
    {
        using var env = new TestEnvironment(_ => TestEnvironment.Json("{}"));
        var path = Path.Combine(env.Paths.CachePath, "m", "fanart.json");
        using var provider = new TestProvider(env);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => provider.Ensure<MovieRootObject>("1", "movies", path)));

        Assert.Equal(1, env.Handler.CallCount);
    }

    private static ImageBuilder Image(int width = 0, string language = null, double rating = 0, int votes = 0)
        => new(width, language, rating, votes);

    private TestProvider CreateProvider() => new(_env);

    internal sealed record ImageBuilder(int Width, string Language, double Rating, int Votes)
    {
        public static implicit operator RemoteImageInfo(ImageBuilder b) => new()
        {
            Width = b.Width,
            Language = b.Language,
            CommunityRating = b.Rating,
            VoteCount = b.Votes,
        };
    }

    private sealed class TestProvider : FanartProvider
    {
        public TestProvider(TestEnvironment env)
            : base(env.Config, env.HttpClientFactory, env.FileSystem, NullLogger.Instance)
        {
        }

        public override int Order => 0;

        public static IEnumerable<RemoteImageInfo> Sort(IEnumerable<ImageBuilder> images, string preferredLanguage)
            => SortImages(images.Select(i => (RemoteImageInfo)i), preferredLanguage);

        public static RemoteImageInfo Create(string url, string likes, string language, ImageType type, int width, int height)
            => CreateImageInfo(url, likes, language, type, width, height);

        public override bool Supports(BaseItem item) => true;

        public override IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [];

        public override Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RemoteImageInfo>>([]);

        public Task<T> Load<T>(string path)
            where T : class
            => TryLoad<T>(path, TestContext.Current.CancellationToken);

        public Task Ensure<T>(string id, string category, string path, CancellationToken? cancellationToken = null)
            where T : new()
            => EnsureJson<T>(id, category, path, cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
