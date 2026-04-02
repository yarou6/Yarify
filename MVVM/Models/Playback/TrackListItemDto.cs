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
    public int ArtistUserId { get; set; }
    public int? AlbumId { get; set; }

    public string DurationText => TimeSpan.FromSeconds(Math.Max(DurationSec, 0)).ToString(@"mm\:ss");
    public string? CoverSource => string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
    public string Source => !string.IsNullOrWhiteSpace(LocalPath) ? LocalPath! : (StreamUrl ?? string.Empty);
}
