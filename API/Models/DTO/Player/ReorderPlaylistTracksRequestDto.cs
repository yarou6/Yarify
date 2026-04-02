using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ReorderPlaylistTracksRequestDto
{
    [Required]
    public IReadOnlyList<int> SongIdsInOrder { get; set; } = Array.Empty<int>();
}
