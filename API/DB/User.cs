using System;
using System.Collections.Generic;

namespace API.DB;

public partial class User
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? ArtistName { get; set; }

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }

    public string? AvatarPath { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLogin { get; set; }

    public virtual ICollection<Album> Albums { get; set; } = new List<Album>();

    public virtual ICollection<Follow> FollowArtistUsers { get; set; } = new List<Follow>();

    public virtual ICollection<Follow> FollowSubscriberUsers { get; set; } = new List<Follow>();

    public virtual ICollection<Likedsong> Likedsongs { get; set; } = new List<Likedsong>();

    public virtual ICollection<Listeningevent> Listeningevents { get; set; } = new List<Listeningevent>();

    public virtual ICollection<Playbackqueue> Playbackqueues { get; set; } = new List<Playbackqueue>();

    public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    public virtual ICollection<Refreshtoken> Refreshtokens { get; set; } = new List<Refreshtoken>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Song> Songs { get; set; } = new List<Song>();

    public virtual Userplan? Userplan { get; set; }
}

