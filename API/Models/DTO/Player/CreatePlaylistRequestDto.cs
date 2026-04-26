using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class CreatePlaylistRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? CoverPath { get; set; }

    public bool IsPublic { get; set; }
}
