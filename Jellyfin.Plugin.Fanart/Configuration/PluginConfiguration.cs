using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fanart.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the personal API key for Fanart.tv.
    /// </summary>
    public string PersonalApiKey { get; set; }
}
