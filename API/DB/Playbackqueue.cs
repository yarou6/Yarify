using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Playbackqueue
{
    public long Id { get; set; }

    public int UserId { get; set; }

    public int SongId { get; set; }

    public int Position { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Song Song { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

