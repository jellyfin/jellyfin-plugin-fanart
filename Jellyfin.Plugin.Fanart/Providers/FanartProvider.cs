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
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fanart.Providers;

/// <summary>
/// Base class for Fanart.tv providers.
/// </summary>
public abstract class FanartProvider : IRemoteImageProvider, IHasOrder, IDisposable
{
    private readonly SemaphoreSlim _ensureSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FanartProvider"/> class.
    /// </summary>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    protected FanartProvider(IServerConfigurationManager config, IHttpClientFactory httpClientFactory, IFileSystem fileSystem, ILogger logger)
    {
        Config = config;
        HttpClientFactory = httpClientFactory;
        FileSystem = fileSystem;
        Logger = logger;
    }

    /// <inheritdoc />
    public string Name => Constants.PluginName;

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <summary>
    /// Gets the server configuration manager.
    /// </summary>
    protected IServerConfigurationManager Config { get; }

    /// <summary>
    /// Gets the HTTP client factory.
    /// </summary>
    protected IHttpClientFactory HttpClientFactory { get; }

    /// <summary>
    /// Gets the file system.
    /// </summary>
    protected IFileSystem FileSystem { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public abstract bool Supports(BaseItem item);

    /// <inheritdoc />
    public abstract IEnumerable<ImageType> GetSupportedImages(BaseItem item);

    /// <inheritdoc />
    public abstract Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return HttpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
    }

    /// <summary>
    /// Orders images by width, then language match, then rating, then vote count.
    /// </summary>
    /// <param name="images">The images to order.</param>
    /// <param name="preferredLanguage">The preferred metadata language.</param>
    /// <returns>The ordered images.</returns>
    protected static IEnumerable<RemoteImageInfo> SortImages(IEnumerable<RemoteImageInfo> images, string preferredLanguage)
    {
        var isLanguageEn = string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase);

        // Sort first by width to prioritize HD versions
        return images.OrderByDescending(i => i.Width ?? 0)
            .ThenByDescending(i =>
            {
                if (string.Equals(preferredLanguage, i.Language, StringComparison.OrdinalIgnoreCase))
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
    /// Creates the remote image info for a Fanart.tv image.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="likes">The like count as text.</param>
    /// <param name="language">The Fanart.tv language code.</param>
    /// <param name="type">The image type.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The remote image info, or <c>null</c> if the URL is empty.</returns>
    protected static RemoteImageInfo CreateImageInfo(string url, string likes, string language, ImageType type, int width, int height)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // Fanart sometimes uses 00 to denote images without language, Jellyfin expects null or an empty string
        if (string.Equals(language, "00", StringComparison.OrdinalIgnoreCase))
        {
            language = null;
        }

        var info = new RemoteImageInfo
        {
            RatingType = RatingType.Likes,
            Type = type,
            Width = width,
            Height = height,
            ProviderName = Constants.PluginName,
            ThumbnailUrl = url.Replace("/fanart/", "/bigpreview/", StringComparison.OrdinalIgnoreCase),
            Url = url,
            Language = language
        };

        if (!string.IsNullOrEmpty(likes)
            && int.TryParse(likes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var likeCount))
        {
            info.CommunityRating = likeCount;
        }

        return info;
    }

    /// <summary>
    /// Loads a cached Fanart.tv JSON file. A file that cannot be parsed is deleted so the next refresh downloads it again.
    /// </summary>
    /// <param name="path">The path of the cached JSON file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="T">The type of the JSON object.</typeparam>
    /// <returns>The deserialized response, or <c>null</c> if the file could not be read.</returns>
    protected async Task<T> TryLoad<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            FileStream fileStream = AsyncFile.OpenRead(path);

            await using (fileStream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<T>(fileStream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Fanart.tv data in {Path} is corrupt, deleting it", path);
            TryDelete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "Could not read Fanart.tv data from {Path}", path);
        }

        return null;
    }

    /// <summary>
    /// Ensures the cached Fanart.tv JSON for an item exists and is at most two days old, downloading it if not.
    /// </summary>
    /// <param name="id">The Fanart.tv item identifier.</param>
    /// <param name="category">The Fanart.tv API category.</param>
    /// <param name="path">The path of the cached JSON file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="T">The response type, saved as an empty object when the item is not found.</typeparam>
    /// <returns>A task that completes when the cached file is usable.</returns>
    protected async Task EnsureJson<T>(string id, string category, string path, CancellationToken cancellationToken)
        where T : new()
    {
        // One caller at a time, so concurrent callers for the same item (such as every season of a series)
        // find the file fresh instead of downloading it again.
        await _ensureSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsFresh(path))
            {
                return;
            }

            await DownloadJson<T>(id, category, path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex) when (FileSystem.GetFileSystemInfo(path).Exists)
        {
            // A reader has the stale file open, so it cannot be replaced now. Keep using it.
            Logger.LogDebug(ex, "Could not refresh Fanart.tv {Category} data for {Id}, reusing the cached file", category, id);
        }
        finally
        {
            _ensureSemaphore.Release();
        }
    }

    /// <summary>
    /// Releases the resources used by the provider.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ensureSemaphore.Dispose();
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "Could not delete {Path}", path);
        }
    }

    private bool IsFresh(string path)
    {
        var fileInfo = FileSystem.GetFileSystemInfo(path);

        return fileInfo.Exists
            && (DateTime.UtcNow - FileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 2;
    }

    private async Task DownloadJson<T>(string id, string category, string path, CancellationToken cancellationToken)
        where T : new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = string.Format(
            CultureInfo.InvariantCulture,
            Plugin.BaseUrlFormat,
            Plugin.ApiKey,
            id,
            category);

        var clientKey = Plugin.Instance.Configuration.PersonalApiKey;
        if (!string.IsNullOrWhiteSpace(clientKey))
        {
            url += "&client_key=" + Uri.EscapeDataString(clientKey);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));

        Logger.LogDebug("Downloading Fanart.tv {Category} data for {Id}", category, id);

        try
        {
            var httpClient = HttpClientFactory.CreateClient(NamedClient.Default);
            using var httpResponse = await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            using var response = httpResponse.Content;
            FileStream fileStream = AsyncFile.Create(path);

            await using (fileStream.ConfigureAwait(false))
            {
                await response.CopyToAsync(fileStream, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // If the user has automatic updates enabled, save a dummy object to prevent repeated download attempts
            Logger.LogDebug("Fanart.tv has no {Category} data for {Id}, caching an empty response", category, id);

            FileStream fileStream = AsyncFile.Create(path);

            await using (fileStream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(fileStream, new T(), JsonDefaults.Options, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
