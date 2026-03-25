using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Songstatsdaily
{
    public long Id { get; set; }

    public DateOnly StartDate { get; set; }

    public int SongId { get; set; }

    public int PlaysCount { get; set; }

    public int UniqueListeners { get; set; }

    public virtual Song Song { get; set; } = null!;
}

