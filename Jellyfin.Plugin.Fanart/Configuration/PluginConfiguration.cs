using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fanart.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string PersonalApiKey { get; set; }
}
