using System.Timers;
using NAudio.Wave;

namespace MVVM.Services;

public sealed class NAudioPlayerService : IAudioPlayerService
{
    private IWavePlayer? _output;
    private WaveStream? _reader;
    private readonly System.Timers.Timer _positionTimer;
    private IWavePlayer? _stopRequestedOutput;

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

    // Готовит и возвращает нужные данные.
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

    // Управляет воспроизведением в плеере.
    public void Play()
    {
        if (_output is null)
            return;

        _output.Play();
        _positionTimer.Start();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Управляет воспроизведением в плеере.
    public void Pause()
    {
        if (_output is null)
            return;

        _output.Pause();
        _positionTimer.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Управляет воспроизведением в плеере.
    public void Stop()
    {
        if (_output is null)
            return;

        _stopRequestedOutput = _output;
        _output.Stop();
        _positionTimer.Stop();
        if (_reader is not null)
            _reader.CurrentTime = TimeSpan.Zero;

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Управляет воспроизведением в плеере.
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

    // Выполняет внутреннюю логику метода.
    public void Dispose()
    {
        _positionTimer.Stop();
        _positionTimer.Dispose();
        DisposePlayback();
    }

    // Обрабатывает событие и запускает нужное действие.
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not IWavePlayer output || !ReferenceEquals(output, _output))
            return;

        _positionTimer.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);

        var wasStopRequested = ReferenceEquals(output, _stopRequestedOutput);
        if (wasStopRequested)
            _stopRequestedOutput = null;

        if (wasStopRequested || e.Exception is not null || _reader is null)
            return;

        var remaining = _reader.TotalTime - _reader.CurrentTime;
        if (remaining <= TimeSpan.FromMilliseconds(350))
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    // Выполняет внутреннюю логику метода.
    private void DisposePlayback()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Dispose();
            _output = null;
        }

        _stopRequestedOutput = null;
        _reader?.Dispose();
        _reader = null;
    }
}
