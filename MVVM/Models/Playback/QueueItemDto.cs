namespace MVVM.Models.Playback;

public sealed class QueueItemDto
{
    public long QueueId { get; set; }
    public int Position { get; set; }
    public TrackListItemDto Track { get; set; } = new();

    public string DisplayTitle => $"{Position}. {Track.Title}";
}
