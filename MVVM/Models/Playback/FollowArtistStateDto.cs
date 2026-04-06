namespace MVVM.Models.Playback;

public sealed class FollowArtistStateDto
{
    public int ArtistUserId { get; set; }
    public bool IsFollowing { get; set; }
    public int FollowersCount { get; set; }
}
