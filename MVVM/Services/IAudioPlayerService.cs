namespace MVVM.Services;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    event EventHandler? TrackEnded;

    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }

    void Load(string source);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
}
