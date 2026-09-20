using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fanart.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using Moq;

namespace Jellyfin.Plugin.Fanart.Tests;

/// <summary>
/// Temp-directory backed fakes for the services the providers use.
/// </summary>
internal sealed class TestEnvironment : IDisposable
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public TestEnvironment(Func<HttpRequestMessage, HttpResponseMessage> respond = null)
    {
        _respond = respond ?? (_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Root = Path.Combine(Path.GetTempPath(), "fanart-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        var paths = new Mock<IServerApplicationPaths>();
        paths.SetupGet(p => p.CachePath).Returns(Path.Combine(Root, "cache"));
        paths.SetupGet(p => p.PluginConfigurationsPath).Returns(Path.Combine(Root, "plugins"));
        paths.SetupGet(p => p.PluginsPath).Returns(Path.Combine(Root, "plugins"));
        paths.SetupGet(p => p.DataPath).Returns(Path.Combine(Root, "data"));
        Paths = paths.Object;

        var config = new Mock<IServerConfigurationManager>();
        config.SetupGet(c => c.ApplicationPaths).Returns(Paths);
        config.SetupGet(c => c.CommonApplicationPaths).Returns(Paths);
        Config = config.Object;

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.GetFileSystemInfo(It.IsAny<string>()))
            .Returns((string p) => new FileSystemMetadata { FullName = p, Exists = File.Exists(p) });
        fileSystem.Setup(f => f.GetLastWriteTimeUtc(It.IsAny<FileSystemMetadata>()))
            .Returns((FileSystemMetadata m) => File.GetLastWriteTimeUtc(m.FullName));
        FileSystem = fileSystem.Object;

        Handler = new FakeHandler(this);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(NamedClient.Default)).Returns(() => new HttpClient(Handler, false));
        HttpClientFactory = factory.Object;

        // Providers read the personal API key from the static plugin instance.
        var xml = new Mock<IXmlSerializer>();
        xml.Setup(x => x.DeserializeFromFile(It.IsAny<Type>(), It.IsAny<string>()))
            .Returns(() => new PluginConfiguration());
        Plugin = new Plugin(Paths, xml.Object);
    }

    public string Root { get; }

    public IServerApplicationPaths Paths { get; }

    public IServerConfigurationManager Config { get; }

    public IFileSystem FileSystem { get; }

    public IHttpClientFactory HttpClientFactory { get; }

    public Plugin Plugin { get; }

    public FakeHandler Handler { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
        }
    }

    public string WriteCacheFile(string relativePath, string content, DateTime? lastWriteUtc = null)
    {
        var path = Path.Combine(Paths.CachePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content, Encoding.UTF8);
        if (lastWriteUtc.HasValue)
        {
            File.SetLastWriteTimeUtc(path, lastWriteUtc.Value);
        }

        return path;
    }

    public static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly TestEnvironment _owner;
        private int _callCount;

        public FakeHandler(TestEnvironment owner)
        {
            _owner = owner;
        }

        public int CallCount => _callCount;

        public Uri LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            LastUri = request.RequestUri;
            return Task.FromResult(_owner._respond(request));
        }
    }
}
