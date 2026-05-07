using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class PlaylistListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }
    public int TracksCount { get; set; }
    public bool ContainsCurrentTrack { get; set; }

    public string? CoverSource => string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
    public object? CoverImage => (object?)CoverBitmap ?? CoverSource;
    public string Subtitle => string.IsNullOrWhiteSpace(Description) ? $"Треков: {TracksCount}" : Description!;
    public string AddToTrackMenuTitle => ContainsCurrentTrack ? $"{Title} (уже есть)" : Title;
}
