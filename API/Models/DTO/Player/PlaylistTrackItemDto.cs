namespace API.Models.DTO;

public sealed class PlaylistTrackItemDto
{
    public int Position { get; set; }
    public TrackListItemDto Track { get; set; } = new();
}
