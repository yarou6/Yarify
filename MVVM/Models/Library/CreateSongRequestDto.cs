namespace MVVM.Models.Library;

public sealed class CreateSongRequestDto
{
    public int? AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationSec { get; set; }
    public string SourceType { get; set; } = "Local";
    public string? LocalPath { get; set; }
    public string? StreamUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? CoverPath { get; set; }
    public int? TrackNumber { get; set; }
    public bool Explicit { get; set; }
    public IReadOnlyList<int> GenreIds { get; set; } = Array.Empty<int>();
}
