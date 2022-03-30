using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class MovieImage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("lang")]
        public string Language { get; set; }

        [JsonPropertyName("likes")]
        public string Likes { get; set; }
    }
}
