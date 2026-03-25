using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Album
{
    public int Id { get; set; }

    public int ArtistUserId { get; set; }

    public string Title { get; set; } = null!;

    public string? CoverPath { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User ArtistUser { get; set; } = null!;

    public virtual ICollection<Song> Songs { get; set; } = new List<Song>();
}

