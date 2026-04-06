namespace MVVM.Models.Playback;

public sealed class PlayerSettingsSnapshot
{
    public double Volume { get; set; } = 0.7;
    public bool IsMuted { get; set; }
    public bool AllowExplicitContent { get; set; } = true;
}
