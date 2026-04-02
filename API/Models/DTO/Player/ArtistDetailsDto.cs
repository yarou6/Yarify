namespace API.Models.DTO;

public sealed class ArtistDetailsDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public int FollowersCount { get; set; }
    public bool IsFollowing { get; set; }
    public IReadOnlyList<TrackListItemDto> TopTracks { get; set; } = Array.Empty<TrackListItemDto>();
    public IReadOnlyList<AlbumListItemDto> Albums { get; set; } = Array.Empty<AlbumListItemDto>();
}
