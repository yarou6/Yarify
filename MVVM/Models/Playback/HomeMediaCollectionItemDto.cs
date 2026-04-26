using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class HomeMediaCollectionItemDto
{
    public string Kind { get; set; } = "album";
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }

    public string? CoverSource => string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
    public object? CoverImage => (object?)CoverBitmap ?? CoverSource;
}
