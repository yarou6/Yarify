namespace MVVM.Models.Playback;

public sealed class OverviewGenreShelfDto
{
    public string Genre { get; set; } = string.Empty;
    public IReadOnlyList<TrackListItemDto> Tracks { get; set; } = Array.Empty<TrackListItemDto>();
}
