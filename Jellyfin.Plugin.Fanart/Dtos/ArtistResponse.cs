using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

/// <summary>
/// Artist response from Fanart.tv API.
/// </summary>
public class ArtistResponse
{
    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the MusicBrainz identifier.
    /// </summary>
    [JsonPropertyName("mbid_id")]
    public string MusicBrainzId { get; set; }

    /// <summary>
    /// Gets or sets the artist thumbnail images.
    /// </summary>
    [JsonPropertyName("artistthumb")]
    public List<ArtistImage> ArtistThumbs { get; set; }

    /// <summary>
    /// Gets or sets the artist background images.
    /// </summary>
    [JsonPropertyName("artistbackground")]
    public List<ArtistImage> ArtistBackgrounds { get; set; }

    /// <summary>
    /// Gets or sets the HD music logo images.
    /// </summary>
    [JsonPropertyName("hdmusiclogo")]
    public List<ArtistImage> HdMusicLogos { get; set; }

    /// <summary>
    /// Gets or sets the music banner images.
    /// </summary>
    [JsonPropertyName("musicbanner")]
    public List<ArtistImage> MusicBanners { get; set; }

    /// <summary>
    /// Gets or sets the music logo images.
    /// </summary>
    [JsonPropertyName("musiclogo")]
    public List<ArtistImage> MusicLogos { get; set; }

    /// <summary>
    /// Gets or sets the music art images.
    /// </summary>
    [JsonPropertyName("musicarts")]
    public List<ArtistImage> MusicArts { get; set; }

    /// <summary>
    /// Gets or sets the HD music art images.
    /// </summary>
    [JsonPropertyName("hdmusicarts")]
    public List<ArtistImage> HdmusicArts { get; set; }

    /// <summary>
    /// Gets or sets the albums.
    /// </summary>
    [JsonPropertyName("albums")]
    public List<Album> Albums { get; set; }
}
