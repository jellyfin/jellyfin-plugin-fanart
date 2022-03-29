using System.Collections.Generic;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class ArtistResponse
    {
        public string name { get; set; }

        public string mbid_id { get; set; }

        public List<ArtistImage> artistthumb { get; set; }

        public List<ArtistImage> artistbackground { get; set; }

        public List<ArtistImage> hdmusiclogo { get; set; }

        public List<ArtistImage> musicbanner { get; set; }

        public List<ArtistImage> musiclogo { get; set; }

        public List<ArtistImage> musicarts { get; set; }

        public List<ArtistImage> hdmusicarts { get; set; }

        public Dictionary<string, Album> albums { get; set; }
    }
}
