using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Songlyric
{
    public int SongId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string LyricsText { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Song Song { get; set; } = null!;
}

