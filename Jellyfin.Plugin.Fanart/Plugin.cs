using System;
using System.Collections.Generic;
using System.Text;
using Jellyfin.Plugin.Fanart.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Fanart;

/// <summary>
/// The Fanart plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly Guid _id = new("170a157f-ac6c-437a-abdd-ca9c25cebd39");

    /// <summary>
    /// The Fanart.tv API key.
    /// </summary>
    public const string ApiKey = "184e1a2b1fe3b94935365411f919f638";

    /// <summary>
    /// The base URL for the Fanart.tv API.
    /// </summary>
    public const string BaseUrl = "https://webservice.fanart.tv/v3.2/{2}/{1}?api_key={0}";

    /// <summary>
    /// The cached composite format for the base URL.
    /// </summary>
    public static readonly CompositeFormat BaseUrlFormat = CompositeFormat.Parse(BaseUrl);

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Fanart";

    /// <inheritdoc />
    public override Guid Id => _id;

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "fanart",
                EmbeddedResourcePath = GetType().Namespace + ".Web.fanart.html",
            },
            new PluginPageInfo
            {
                Name = "fanartjs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.fanart.js"
            }
        ];
    }
}
