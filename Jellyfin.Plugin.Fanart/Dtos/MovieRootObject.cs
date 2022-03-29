using System.Collections.Generic;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class MovieRootObject
    {
        public string name { get; set; }

        public string tmdb_id { get; set; }

        public string imdb_id { get; set; }

        public List<MovieImage> hdmovielogo { get; set; }

        public List<MovieImage> moviedisc { get; set; }

        public List<MovieImage> movielogo { get; set; }

        public List<MovieImage> movieposter { get; set; }

        public List<MovieImage> hdmovieclearart { get; set; }

        public List<MovieImage> movieart { get; set; }

        public List<MovieImage> moviebackground { get; set; }

        public List<MovieImage> moviebanner { get; set; }

        public List<MovieImage> moviethumb { get; set; }
    }
}
