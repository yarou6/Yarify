namespace API.Models.DTO;

public sealed class PlaylistListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string? CoverPath { get; set; }
    public int TracksCount { get; set; }
}
