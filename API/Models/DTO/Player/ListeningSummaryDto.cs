namespace API.Models.DTO;

public sealed class ListeningSummaryDto
{
    public int Days { get; set; }
    public int PlaysCount { get; set; }
    public int CompletedCount { get; set; }
    public int TotalPlayedMs { get; set; }
    public int UniqueTracks { get; set; }
    public int UniqueArtists { get; set; }
}
