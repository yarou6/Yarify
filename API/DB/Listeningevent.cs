using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Listeningevent
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    public int SongId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int PlayedMs { get; set; }

    public bool Completed { get; set; }

    public string? SourceType { get; set; }

    public int? SourceId { get; set; }

    public virtual Song Song { get; set; } = null!;

    public virtual User? User { get; set; }
}

