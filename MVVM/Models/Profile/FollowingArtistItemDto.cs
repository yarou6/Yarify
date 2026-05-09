using Avalonia.Media.Imaging;

namespace MVVM.Models.Profile;

public sealed class FollowingArtistItemDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public Bitmap? AvatarBitmap { get; set; }
    public object? AvatarImage => (object?)AvatarBitmap ?? AvatarPath;
    public DateTime FollowedAt { get; set; }
    public int FollowersCount { get; set; }
}
