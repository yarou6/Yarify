using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdateProfileRequestDto
{
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ArtistName { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }
}
