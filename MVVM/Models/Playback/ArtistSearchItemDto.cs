using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class ArtistSearchItemDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public Bitmap? AvatarBitmap { get; set; }
    public int TracksCount { get; set; }

    public string? AvatarSource => string.IsNullOrWhiteSpace(AvatarPath) ? null : AvatarPath;
    public object? AvatarImage => (object?)AvatarBitmap ?? AvatarSource;
    public string TracksCountText => $"{Math.Max(0, TracksCount)} треков";
}
