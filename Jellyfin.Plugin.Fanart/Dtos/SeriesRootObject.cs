using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

/// <summary>
/// Series response from Fanart.tv API.
/// </summary>
public class SeriesRootObject
{
    /// <summary>
    /// Gets or sets the total image count.
    /// </summary>
    [JsonPropertyName("image_count")]
    public int ImageCount { get; set; }

    /// <summary>
    /// Gets or sets the series name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the TVDB identifier.
    /// </summary>
    [JsonPropertyName("thetvdb_id")]
    public string TheTvDbId { get; set; }

    /// <summary>
    /// Gets or sets the clear logo images.
    /// </summary>
    [JsonPropertyName("clearlogo")]
    public List<SeriesImage> ClearLogos { get; set; }

    /// <summary>
    /// Gets or sets the HD TV logo images.
    /// </summary>
    [JsonPropertyName("hdtvlogo")]
    public List<SeriesImage> HdTvLogos { get; set; }

    /// <summary>
    /// Gets or sets the clear art images.
    /// </summary>
    [JsonPropertyName("clearart")]
    public List<SeriesImage> ClearArts { get; set; }

    /// <summary>
    /// Gets or sets the show background images.
    /// </summary>
    [JsonPropertyName("showbackground")]
    public List<SeriesImage> Showbackgrounds { get; set; }

    /// <summary>
    /// Gets or sets the TV thumbnail images.
    /// </summary>
    [JsonPropertyName("tvthumb")]
    public List<SeriesImage> TvThumbs { get; set; }

    /// <summary>
    /// Gets or sets the season poster images.
    /// </summary>
    [JsonPropertyName("seasonposter")]
    public List<SeriesImage> SeasonPosters { get; set; }

    /// <summary>
    /// Gets or sets the season thumbnail images.
    /// </summary>
    [JsonPropertyName("seasonthumb")]
    public List<SeriesImage> SeasonThumbs { get; set; }

    /// <summary>
    /// Gets or sets the HD clear art images.
    /// </summary>
    [JsonPropertyName("hdclearart")]
    public List<SeriesImage> HdClearArts { get; set; }

    /// <summary>
    /// Gets or sets the TV banner images.
    /// </summary>
    [JsonPropertyName("tvbanner")]
    public List<SeriesImage> TvBanners { get; set; }

    /// <summary>
    /// Gets or sets the character art images.
    /// </summary>
    [JsonPropertyName("characterart")]
    public List<SeriesImage> CharacterArts { get; set; }

    /// <summary>
    /// Gets or sets the TV poster images.
    /// </summary>
    [JsonPropertyName("tvposter")]
    public List<SeriesImage> TvPosters { get; set; }

    /// <summary>
    /// Gets or sets the season banner images.
    /// </summary>
    [JsonPropertyName("seasonbanner")]
    public List<SeriesImage> SeasonBanners { get; set; }
}
