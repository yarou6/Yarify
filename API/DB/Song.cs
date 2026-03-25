using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Song
{
    public int Id { get; set; }

    public int ArtistUserId { get; set; }

    public int? AlbumId { get; set; }

    public string Title { get; set; } = null!;

    public int DurationSec { get; set; }

    public string SourceType { get; set; } = null!;

    public string? LocalPath { get; set; }

    public string? StreamUrl { get; set; }

    public string? ExternalId { get; set; }

    public string? CoverPath { get; set; }

    public int? TrackNumber { get; set; }

    public bool Explicit { get; set; }

    public long PlayCount { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Album? Album { get; set; }

    public virtual User ArtistUser { get; set; } = null!;

    public virtual ICollection<Likedsong> Likedsongs { get; set; } = new List<Likedsong>();

    public virtual ICollection<Listeningevent> Listeningevents { get; set; } = new List<Listeningevent>();

    public virtual ICollection<Playbackqueue> Playbackqueues { get; set; } = new List<Playbackqueue>();

    public virtual ICollection<Playlistsong> Playlistsongs { get; set; } = new List<Playlistsong>();

    public virtual ICollection<Songlyric> Songlyrics { get; set; } = new List<Songlyric>();

    public virtual ICollection<Songstatsdaily> Songstatsdailies { get; set; } = new List<Songstatsdaily>();

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();
}

