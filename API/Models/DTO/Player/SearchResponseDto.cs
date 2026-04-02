namespace API.Models.DTO;

public sealed class SearchResponseDto
{
    public IReadOnlyList<TrackListItemDto> Tracks { get; set; } = Array.Empty<TrackListItemDto>();
    public IReadOnlyList<AlbumListItemDto> Albums { get; set; } = Array.Empty<AlbumListItemDto>();
    public IReadOnlyList<PublicArtistItemDto> Artists { get; set; } = Array.Empty<PublicArtistItemDto>();
    public IReadOnlyList<PublicPlaylistItemDto> Playlists { get; set; } = Array.Empty<PublicPlaylistItemDto>();
}
