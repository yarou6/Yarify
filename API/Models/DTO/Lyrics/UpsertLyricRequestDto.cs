using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpsertLyricRequestDto
{
    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "und";

    [Required]
    public string LyricsText { get; set; } = string.Empty;

    [MaxLength(20)]
    public string SourceType { get; set; } = "Manual";
}
