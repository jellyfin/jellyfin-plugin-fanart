using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fanart.Dtos;

public class MovieRootObject
{
    [JsonPropertyName("image_count")]
    public int ImageCount { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tmdb_id")]
    public string TmbdId { get; set; }

    [JsonPropertyName("imdb_id")]
    public string ImdbId { get; set; }

    [JsonPropertyName("hdmovielogo")]
    public List<MovieImage> HdMovieLogos { get; set; }

    [JsonPropertyName("moviedisc")]
    public List<MovieImage> MovieDiscImages { get; set; }

    [JsonPropertyName("movielogo")]
    public List<MovieImage> MovieLogos { get; set; }

    [JsonPropertyName("movieposter")]
    public List<MovieImage> MoviePosters { get; set; }

    [JsonPropertyName("hdmovieclearart")]
    public List<MovieImage> HdMovieClearArts { get; set; }

    [JsonPropertyName("movieart")]
    public List<MovieImage> MovieArts { get; set; }

    [JsonPropertyName("moviebackground")]
    public List<MovieImage> MovieBackgrounds { get; set; }

    [JsonPropertyName("moviebanner")]
    public List<MovieImage> MovieBanners { get; set; }

    [JsonPropertyName("moviethumb")]
    public List<MovieImage> MovieThumbs { get; set; }
}
