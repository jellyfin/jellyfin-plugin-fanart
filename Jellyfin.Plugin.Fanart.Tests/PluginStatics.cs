using Xunit;

namespace Jellyfin.Plugin.Fanart.Tests;

/// <summary>
/// Plugin.Instance and the provider Current singletons are process-wide, so tests that touch them run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginStatics
{
    public const string Name = "Plugin statics";
}
