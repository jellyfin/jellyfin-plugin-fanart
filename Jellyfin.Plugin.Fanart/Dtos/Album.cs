using System.Collections.Generic;

namespace Jellyfin.Plugin.Fanart.Dtos
{
    public class Album
    {
        public List<ArtistImage> cdart { get; set; }

        public List<ArtistImage> albumcover { get; set; }
    }
}
