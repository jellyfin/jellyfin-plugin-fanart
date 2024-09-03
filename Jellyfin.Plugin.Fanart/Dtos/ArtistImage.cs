using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class ArtistImage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("added")]
        public string Added { get; set; }

        [JsonPropertyName("likes")]
        public string Likes { get; set; }

        [JsonPropertyName("disc")]
        public string Disc { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }

        [JsonPropertyName("lang")]
        public string Language { get; set; }
    }
}
