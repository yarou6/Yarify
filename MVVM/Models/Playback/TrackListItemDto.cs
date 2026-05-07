using Avalonia.Media.Imaging;

namespace MVVM.Models.Playback;

public sealed class TrackListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationSec { get; set; }
    public string? StreamUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? CoverPath { get; set; }
    public Bitmap? CoverBitmap { get; set; }
    public int ArtistUserId { get; set; }
    public int? AlbumId { get; set; }
    public string? AlbumTitle { get; set; }
    public int? TrackOrder { get; set; }
    public int PlayCount { get; set; }
    public bool Explicit { get; set; }
    public IReadOnlyList<string> GenreTitles { get; set; } = Array.Empty<string>();

    public string DurationText => TimeSpan.FromSeconds(Math.Max(DurationSec, 0)).ToString(@"mm\:ss");
    public string PlaysText => $"{Math.Max(0, PlayCount):N0}";
    public string? CoverSource => string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
    public object? CoverImage => (object?)CoverBitmap ?? CoverSource;
    public string TrackOrderText => TrackOrder?.ToString() ?? "•";
    public string Source => !string.IsNullOrWhiteSpace(LocalPath) ? LocalPath! : (StreamUrl ?? string.Empty);
    public string AlbumBadgeText => !string.IsNullOrWhiteSpace(AlbumTitle)
        ? $"Из альбома: {AlbumTitle}"
        : AlbumId.HasValue
            ? "Из альбома"
            : "Сингл";
    public string ReleaseAndSourceText => !string.IsNullOrWhiteSpace(AlbumTitle)
        ? $"Релиз: {AlbumTitle}"
        : "Релиз: сингл";
}
