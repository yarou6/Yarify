namespace API.Models.DTO;

public sealed class PlaybackSettingsDto
{
    public bool ShuffleEnabled { get; set; }
    public string RepeatMode { get; set; } = "Off";
    public bool AutoplayEnabled { get; set; } = true;
}
