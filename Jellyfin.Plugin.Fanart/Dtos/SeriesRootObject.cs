using System.Collections.Generic;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class SeriesRootObject
    {
        public string name { get; set; }

        public string thetvdb_id { get; set; }

        public List<SeriesImage> clearlogo { get; set; }

        public List<SeriesImage> hdtvlogo { get; set; }

        public List<SeriesImage> clearart { get; set; }

        public List<SeriesImage> showbackground { get; set; }

        public List<SeriesImage> tvthumb { get; set; }

        public List<SeriesImage> seasonposter { get; set; }

        public List<SeriesImage> seasonthumb { get; set; }

        public List<SeriesImage> hdclearart { get; set; }

        public List<SeriesImage> tvbanner { get; set; }

        public List<SeriesImage> characterart { get; set; }

        public List<SeriesImage> tvposter { get; set; }

        public List<SeriesImage> seasonbanner { get; set; }
    }
}
