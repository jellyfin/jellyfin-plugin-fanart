using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class Album
    {
        [JsonPropertyName("release_group_id")]
        public string ReleaseGroupId { get; set; }

        [JsonPropertyName("cdart")]
        public List<ArtistImage> CdArts { get; set; }

        [JsonPropertyName("albumcover")]
        public List<ArtistImage> AlbumCovers { get; set; }
    }
}
