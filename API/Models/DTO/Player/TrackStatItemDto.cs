namespace API.Models.DTO;

public sealed class TrackStatItemDto
{
    public TrackListItemDto Track { get; set; } = new();
    public int PlaysCount { get; set; }
    public int UniqueListeners { get; set; }
}
