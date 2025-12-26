using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

/// <summary>
/// Movie response from Fanart.tv API.
/// </summary>
public class MovieRootObject
{
    /// <summary>
    /// Gets or sets the total image count.
    /// </summary>
    [JsonPropertyName("image_count")]
    public int ImageCount { get; set; }

    /// <summary>
    /// Gets or sets the movie name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the TMDB identifier.
    /// </summary>
    [JsonPropertyName("tmdb_id")]
    public string TmbdId { get; set; }

    /// <summary>
    /// Gets or sets the IMDB identifier.
    /// </summary>
    [JsonPropertyName("imdb_id")]
    public string ImdbId { get; set; }

    /// <summary>
    /// Gets or sets the HD movie logo images.
    /// </summary>
    [JsonPropertyName("hdmovielogo")]
    public List<MovieImage> HdMovieLogos { get; set; }

    /// <summary>
    /// Gets or sets the movie disc images.
    /// </summary>
    [JsonPropertyName("moviedisc")]
    public List<MovieImage> MovieDiscImages { get; set; }

    /// <summary>
    /// Gets or sets the movie logo images.
    /// </summary>
    [JsonPropertyName("movielogo")]
    public List<MovieImage> MovieLogos { get; set; }

    /// <summary>
    /// Gets or sets the movie poster images.
    /// </summary>
    [JsonPropertyName("movieposter")]
    public List<MovieImage> MoviePosters { get; set; }

    /// <summary>
    /// Gets or sets the HD movie clear art images.
    /// </summary>
    [JsonPropertyName("hdmovieclearart")]
    public List<MovieImage> HdMovieClearArts { get; set; }

    /// <summary>
    /// Gets or sets the movie art images.
    /// </summary>
    [JsonPropertyName("movieart")]
    public List<MovieImage> MovieArts { get; set; }

    /// <summary>
    /// Gets or sets the movie background images.
    /// </summary>
    [JsonPropertyName("moviebackground")]
    public List<MovieImage> MovieBackgrounds { get; set; }

    /// <summary>
    /// Gets or sets the movie banner images.
    /// </summary>
    [JsonPropertyName("moviebanner")]
    public List<MovieImage> MovieBanners { get; set; }

    /// <summary>
    /// Gets or sets the movie thumbnail images.
    /// </summary>
    [JsonPropertyName("moviethumb")]
    public List<MovieImage> MovieThumbs { get; set; }
}
