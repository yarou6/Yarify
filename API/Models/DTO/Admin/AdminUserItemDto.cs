namespace API.Models.DTO;

public sealed class AdminUserItemDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public bool IsActive { get; set; }
    public string RoleTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
