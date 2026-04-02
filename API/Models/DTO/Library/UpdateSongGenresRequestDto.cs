using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdateSongGenresRequestDto
{
    [Required]
    public IReadOnlyList<int> GenreIds { get; set; } = Array.Empty<int>();
}
