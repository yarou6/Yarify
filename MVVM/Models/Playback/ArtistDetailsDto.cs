namespace MVVM.Models.Playback;

public sealed class ArtistDetailsDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public IReadOnlyList<TrackListItemDto> TopTracks { get; set; } = Array.Empty<TrackListItemDto>();
    public IReadOnlyList<AlbumListItemDto> Albums { get; set; } = Array.Empty<AlbumListItemDto>();
}
