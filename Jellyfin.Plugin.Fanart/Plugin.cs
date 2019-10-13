using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Fanart.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Fanart
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public const string ApiKey = "184e1a2b1fe3b94935365411f919f638";
        public const string BaseUrl = "https://webservice.fanart.tv/v3/{2}/{1}?api_key={0}";

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override string Name => "Fanart";

        /// <inheritdoc />
        public override Guid Id { get; } = new Guid("170a157f-ac6c-437a-abdd-ca9c25cebd39");

        public static Plugin Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace)
                }
            };
        }
    }
}
