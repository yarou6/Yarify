using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ListeningEventProgressRequestDto
{
    [Range(0, int.MaxValue)]
    public int PlayedMs { get; set; }

    public DateTime? EndedAt { get; set; }
}
