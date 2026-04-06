using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class AlbumListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    public string YearText => ReleaseDate?.Year.ToString() ?? "-";
    public string? CoverSource => string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
    public object? CoverImage => (object?)CoverBitmap ?? CoverSource;
}
