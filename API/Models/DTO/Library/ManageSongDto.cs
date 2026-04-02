namespace API.Models.DTO;

public sealed class ManageSongDto
{
    public int Id { get; set; }
    public int ArtistUserId { get; set; }
    public int? AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationSec { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public string? StreamUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? CoverPath { get; set; }
    public int? TrackNumber { get; set; }
    public bool Explicit { get; set; }
    public long PlayCount { get; set; }
    public bool IsActive { get; set; }
    public List<GenreItemDto> Genres { get; set; } = new();
}
