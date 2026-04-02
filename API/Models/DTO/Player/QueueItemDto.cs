namespace API.Models.DTO;

public sealed class QueueItemDto
{
    public long QueueId { get; set; }
    public int Position { get; set; }
    public TrackListItemDto Track { get; set; } = new();
}
