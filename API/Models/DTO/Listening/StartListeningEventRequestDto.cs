using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class StartListeningEventRequestDto
{
    [Range(1, int.MaxValue)]
    public int SongId { get; set; }

    [MaxLength(20)]
    public string? SourceType { get; set; }

    public int? SourceId { get; set; }

    public DateTime? StartedAt { get; set; }
}
