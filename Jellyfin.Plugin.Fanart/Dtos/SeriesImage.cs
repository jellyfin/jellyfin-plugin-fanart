using System.Collections.Generic;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class SeriesImage
    {
        public string id { get; set; }

        public string url { get; set; }

        public string lang { get; set; }

        public string likes { get; set; }

        public string season { get; set; }
    }
}
