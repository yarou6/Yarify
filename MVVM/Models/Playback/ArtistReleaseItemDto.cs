using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class ArtistReleaseItemDto
{
    public bool IsAlbum { get; set; }
    public int? AlbumId { get; set; }
    public int? TrackId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }
    public int PlaysCount { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    public string TypeText => IsAlbum ? "Альбом" : "Сингл";
    public string YearText => ReleaseDate?.Year.ToString() ?? "-";
    public string PlaysText => $"{Math.Max(0, PlaysCount):N0}";
    public object? CoverImage => (object?)CoverBitmap ?? CoverPath;
}
