using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class ArtistResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("mbid_id")]
        public string MusicBrainzId { get; set; }

        [JsonPropertyName("artistthumb")]
        public List<ArtistImage> ArtistThumbs { get; set; }

        [JsonPropertyName("artistbackground")]
        public List<ArtistImage> ArtistBackgrounds { get; set; }

        [JsonPropertyName("hdmusiclogo")]
        public List<ArtistImage> HdMusicLogos { get; set; }

        [JsonPropertyName("musicbanner")]
        public List<ArtistImage> MusicBanners { get; set; }

        [JsonPropertyName("musiclogo")]
        public List<ArtistImage> MusicLogos { get; set; }

        [JsonPropertyName("musicarts")]
        public List<ArtistImage> MusicArts { get; set; }

        [JsonPropertyName("hdmusicarts")]
        public List<ArtistImage> HdmusicArts { get; set; }

        [JsonPropertyName("albums")]
        public Dictionary<string, Album> Albums { get; set; }
    }
}
