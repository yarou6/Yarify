using System.Timers;
using NAudio.Wave;

namespace MVVM.Services;

public sealed class NAudioPlayerService : IAudioPlayerService
{
    private IWavePlayer? _output;
    private WaveStream? _reader;
    private readonly System.Timers.Timer _positionTimer;

    public NAudioPlayerService()
    {
        _positionTimer = new System.Timers.Timer(200);
        _positionTimer.Elapsed += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? TrackEnded;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public double Volume
    {
        get => _output?.Volume ?? 1.0f;
        set
        {
            if (_output is null)
                return;

            _output.Volume = (float)Math.Clamp(value, 0.0, 1.0);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Load(string source)
    {
        Stop();
        DisposePlayback();

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            _reader = new MediaFoundationReader(source);
        else
            _reader = new AudioFileReader(source);

        _output = new WaveOutEvent();
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_reader);

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        if (_output is null)
            return;

        _output.Play();
        _positionTimer.Start();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_output is null)
            return;

        _output.Pause();
        _positionTimer.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_output is null)
            return;

        _output.Stop();
        _positionTimer.Stop();
        if (_reader is not null)
            _reader.CurrentTime = TimeSpan.Zero;

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null || !_reader.CanSeek)
            return;

        var next = position;
        if (next < TimeSpan.Zero)
            next = TimeSpan.Zero;
        if (next > _reader.TotalTime)
            next = _reader.TotalTime;

        _reader.CurrentTime = next;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _positionTimer.Dispose();
        DisposePlayback();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _positionTimer.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);

        if (_reader is not null && _reader.Position >= _reader.Length)
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void DisposePlayback()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
    }
}

