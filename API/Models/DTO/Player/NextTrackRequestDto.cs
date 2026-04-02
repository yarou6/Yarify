namespace API.Models.DTO;

public sealed class NextTrackRequestDto
{
    public int CurrentSongId { get; set; }
    public long? CurrentQueueId { get; set; }
    public int? PlaylistId { get; set; }
    public int? AlbumId { get; set; }
}
