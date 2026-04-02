namespace API.Models.DTO;

public sealed class PublicArtistItemDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public int FollowersCount { get; set; }
    public int TracksCount { get; set; }
}
