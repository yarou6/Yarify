using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class UpdatePlaybackSettingsRequestDto
{
    public bool ShuffleEnabled { get; set; }

    [Required]
    [MaxLength(10)]
    public string RepeatMode { get; set; } = "Off";

    public bool AutoplayEnabled { get; set; } = true;
}
