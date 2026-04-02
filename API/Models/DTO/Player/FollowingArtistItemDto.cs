namespace API.Models.DTO;

public sealed class FollowingArtistItemDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public DateTime FollowedAt { get; set; }
    public int FollowersCount { get; set; }
}
