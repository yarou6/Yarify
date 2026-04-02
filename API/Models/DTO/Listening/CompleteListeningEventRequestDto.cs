using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class CompleteListeningEventRequestDto
{
    [Range(0, int.MaxValue)]
    public int PlayedMs { get; set; }

    public bool Completed { get; set; } = true;

    public DateTime? EndedAt { get; set; }
}
