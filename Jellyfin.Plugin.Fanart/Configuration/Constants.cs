using System;

namespace Jellyfin.Plugin.Fanart.Configuration;

/// <summary>
/// Plugin constants.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The name of the plugin and its image providers.
    /// </summary>
    public const string PluginName = "Fanart";

    /// <summary>
    /// The date after which thumb image dimensions are reliable.
    /// </summary>
    public static readonly DateTime WorkingThumbImageDimensions = new(2016, 1, 8);
}
