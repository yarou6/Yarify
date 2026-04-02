namespace API.Models.DTO;

public sealed class NextTrackResponseDto
{
    public TrackListItemDto? Track { get; set; }
    public string? Source { get; set; }
    public bool ReachedEnd { get; set; }
}
