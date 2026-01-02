using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

/// <summary>
/// Artist image from Fanart.tv API.
/// </summary>
public class ArtistImage
{
    /// <summary>
    /// Gets or sets the image identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the image URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the number of likes.
    /// </summary>
    [JsonPropertyName("likes")]
    public string Likes { get; set; }

    /// <summary>
    /// Gets or sets the image language.
    /// </summary>
    [JsonPropertyName("lang")]
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the image width.
    /// </summary>
    [JsonPropertyName("width")]
    public string Width { get; set; }

    /// <summary>
    /// Gets or sets the image height.
    /// </summary>
    [JsonPropertyName("height")]
    public string Height { get; set; }

    /// <summary>
    /// Gets or sets the disc number.
    /// </summary>
    [JsonPropertyName("disc")]
    public string Disc { get; set; }

    /// <summary>
    /// Gets or sets the image size.
    /// </summary>
    [JsonPropertyName("size")]
    public string Size { get; set; }

    /// <summary>
    /// Gets or sets the date the image was added.
    /// </summary>
    [JsonPropertyName("added")]
    public string Added { get; set; }
}
