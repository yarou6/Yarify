namespace API.Models.DTO;

public sealed class ProfileMeDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarPath { get; set; }
    public bool IsActive { get; set; }
    public string RoleTitle { get; set; } = string.Empty;
}
