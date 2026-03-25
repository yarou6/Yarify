using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UsersPanelRoleRequestDto
{
    [Required]
    public string ArtistName { get; set; } = null!;
}

