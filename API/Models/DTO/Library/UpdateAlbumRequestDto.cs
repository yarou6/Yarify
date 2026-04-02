using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdateAlbumRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateOnly? ReleaseDate { get; set; }

    [MaxLength(500)]
    public string? CoverPath { get; set; }
}
