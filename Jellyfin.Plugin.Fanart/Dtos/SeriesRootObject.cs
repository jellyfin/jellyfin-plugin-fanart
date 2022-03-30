using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class SeriesRootObject
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("thetvdb_id")]
        public string TheTvDbId { get; set; }

        [JsonPropertyName("clearlogo")]
        public List<SeriesImage> ClearLogos { get; set; }

        [JsonPropertyName("hdtvlogo")]
        public List<SeriesImage> HdTvLogos { get; set; }

        [JsonPropertyName("clearart")]
        public List<SeriesImage> ClearArts { get; set; }

        [JsonPropertyName("showbackground")]
        public List<SeriesImage> Showbackgrounds { get; set; }

        [JsonPropertyName("tvthumb")]
        public List<SeriesImage> TvThumbs { get; set; }

        [JsonPropertyName("seasonposter")]
        public List<SeriesImage> SeasonPosters { get; set; }

        [JsonPropertyName("seasonthumb")]
        public List<SeriesImage> SeasonThumbs { get; set; }

        [JsonPropertyName("hdclearart")]
        public List<SeriesImage> HdClearArts { get; set; }

        [JsonPropertyName("tvbanner")]
        public List<SeriesImage> TvBanners { get; set; }

        [JsonPropertyName("characterart")]
        public List<SeriesImage> CharacterArts { get; set; }

        [JsonPropertyName("tvposter")]
        public List<SeriesImage> TvPosters { get; set; }

        [JsonPropertyName("seasonbanner")]
        public List<SeriesImage> SeasonBanners { get; set; }
    }
}
