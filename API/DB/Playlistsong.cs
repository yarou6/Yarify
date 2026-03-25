using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Playlistsong
{
    public int Id { get; set; }

    public int PlaylistId { get; set; }

    public int SongId { get; set; }

    public int Position { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Playlist Playlist { get; set; } = null!;

    public virtual Song Song { get; set; } = null!;
}

