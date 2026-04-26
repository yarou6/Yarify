namespace MVVM.Models.Playback;

public sealed class ListeningHistoryItemDto
{
    public long EventId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int PlayedMs { get; set; }
    public bool Completed { get; set; }
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public TrackListItemDto Track { get; set; } = new();
}
