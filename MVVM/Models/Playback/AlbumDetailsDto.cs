using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class AlbumDetailsDto
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public IReadOnlyList<TrackListItemDto> Tracks { get; set; } = Array.Empty<TrackListItemDto>();

    public object? CoverImage => (object?)CoverBitmap ?? CoverPath;
}
