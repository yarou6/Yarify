using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Genre
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public virtual ICollection<Song> Songs { get; set; } = new List<Song>();
}

