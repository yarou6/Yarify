using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class BulkPlaylistTracksRequestDto
{
    [Required]
    public IReadOnlyList<int> SongIds { get; set; } = Array.Empty<int>();
}
