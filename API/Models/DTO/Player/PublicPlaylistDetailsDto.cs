namespace API.Models.DTO;

public sealed class PublicPlaylistDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public int TracksCount { get; set; }
    public int OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public IReadOnlyList<PlaylistTrackItemDto> Tracks { get; set; } = Array.Empty<PlaylistTrackItemDto>();
}
