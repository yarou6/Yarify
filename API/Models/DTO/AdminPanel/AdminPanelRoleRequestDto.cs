using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class AdminPanelRoleRequestDto
{
    [Required]
    public string RoleTitle { get; set; } = null!;

    public string? ArtistName { get; set; }
}

