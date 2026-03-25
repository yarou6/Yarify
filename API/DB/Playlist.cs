using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Playlist
{
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public string? CoverPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User OwnerUser { get; set; } = null!;

    public virtual ICollection<Playlistsong> Playlistsongs { get; set; } = new List<Playlistsong>();
}

