using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

/// <summary>
/// Album response from Fanart.tv API.
/// </summary>
public class Album
{
    /// <summary>
    /// Gets or sets the MusicBrainz release group identifier.
    /// </summary>
    [JsonPropertyName("release_group_id")]
    public string ReleaseGroupId { get; set; }

    /// <summary>
    /// Gets or sets the CD art images.
    /// </summary>
    [JsonPropertyName("cdart")]
    public List<ArtistImage> CdArts { get; set; }

    /// <summary>
    /// Gets or sets the album cover images.
    /// </summary>
    [JsonPropertyName("albumcover")]
    public List<ArtistImage> AlbumCovers { get; set; }
}
