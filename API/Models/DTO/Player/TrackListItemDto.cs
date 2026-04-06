namespace API.Models.DTO;

public sealed class TrackListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationSec { get; set; }
    public string? StreamUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? CoverPath { get; set; }
    public int PlayCount { get; set; }
    public int ArtistUserId { get; set; }
    public int? AlbumId { get; set; }
    public string? AlbumTitle { get; set; }
}
