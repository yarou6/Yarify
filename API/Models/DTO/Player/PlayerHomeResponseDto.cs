namespace API.Models.DTO;

public sealed class PlayerHomeResponseDto
{
    public IReadOnlyList<TrackListItemDto> RecentlyPlayed { get; set; } = Array.Empty<TrackListItemDto>();
    public IReadOnlyList<TrackListItemDto> TrendingTracks { get; set; } = Array.Empty<TrackListItemDto>();
    public IReadOnlyList<AlbumListItemDto> NewReleases { get; set; } = Array.Empty<AlbumListItemDto>();
    public IReadOnlyList<ArtistCardItemDto> RecommendedArtists { get; set; } = Array.Empty<ArtistCardItemDto>();
}
