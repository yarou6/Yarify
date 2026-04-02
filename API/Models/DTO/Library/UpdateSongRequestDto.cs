using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdateSongRequestDto
{
    public int? AlbumId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 7200)]
    public int DurationSec { get; set; }

    [MaxLength(20)]
    public string SourceType { get; set; } = "Local";

    [MaxLength(1000)]
    public string? LocalPath { get; set; }

    [MaxLength(1000)]
    public string? StreamUrl { get; set; }

    [MaxLength(200)]
    public string? ExternalId { get; set; }

    [MaxLength(500)]
    public string? CoverPath { get; set; }

    public int? TrackNumber { get; set; }

    public bool Explicit { get; set; }

    public bool IsActive { get; set; }
}
