using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Follow
{
    public int SubscriberUserId { get; set; }

    public int ArtistUserId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User ArtistUser { get; set; } = null!;

    public virtual User SubscriberUser { get; set; } = null!;
}

