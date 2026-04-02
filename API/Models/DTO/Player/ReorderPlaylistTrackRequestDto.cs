using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ReorderPlaylistTrackRequestDto
{
    [Required]
    public int SongId { get; set; }

    [Range(1, int.MaxValue)]
    public int TargetPosition { get; set; }
}
