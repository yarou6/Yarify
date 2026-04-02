using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdatePlaylistRequestDto
{
    [Required]
    [StringLength(180, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1200)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }
}

