using LibVLCSharp.Shared;

namespace MVVM.Services;

public sealed class LibVlcAudioPlayerService : IAudioPlayerService
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private string? _initializationError;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? TrackEnded;

    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

    public TimeSpan Position
    {
        get
        {
            var ms = _mediaPlayer?.Time ?? 0;
            return ms <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
        }
    }

    public TimeSpan Duration
    {
        get
        {
            var length = _mediaPlayer?.Length ?? 0;
            if (length > 0)
                return TimeSpan.FromMilliseconds(length);

            var mediaDuration = _media?.Duration ?? 0;
            return mediaDuration > 0 ? TimeSpan.FromMilliseconds(mediaDuration) : TimeSpan.Zero;
        }
    }

    public double Volume
    {
        get => Math.Clamp((_mediaPlayer?.Volume ?? 100) / 100d, 0.0, 1.0);
        set
        {
            if (_mediaPlayer is null)
                return;

            _mediaPlayer.Volume = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 100);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Load(string source)
    {
        Stop();
        DisposeMedia();
        EnsureInitialized();

        if (_libVlc is null || _mediaPlayer is null)
            throw new InvalidOperationException(_initializationError ?? "LibVLC не инициализирован.");

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            _media = new Media(_libVlc, uri);
        else
            _media = new Media(_libVlc, source, FromType.FromPath);

        _media.Parse(MediaParseOptions.ParseLocal);
        _mediaPlayer.Media = _media;

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        if (_mediaPlayer is null)
            return;

        _mediaPlayer.Play();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_mediaPlayer is null)
            return;

        _mediaPlayer.Pause();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_mediaPlayer is null)
            return;

        _mediaPlayer.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (_mediaPlayer is null || !_mediaPlayer.IsSeekable)
            return;

        var durationMs = Duration.TotalMilliseconds;
        var requestedMs = Math.Max(0, position.TotalMilliseconds);
        var clampedMs = durationMs > 0 ? Math.Min(requestedMs, durationMs) : requestedMs;
        _mediaPlayer.Time = (long)Math.Round(clampedMs);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Playing -= OnPlaybackStateChanged;
            _mediaPlayer.Paused -= OnPlaybackStateChanged;
            _mediaPlayer.Stopped -= OnPlaybackStateChanged;
            _mediaPlayer.TimeChanged -= OnTimeChanged;
            _mediaPlayer.EndReached -= OnEndReached;
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }

        DisposeMedia();
        _libVlc?.Dispose();
        _libVlc = null;
    }

    private void EnsureInitialized()
    {
        if (_mediaPlayer is not null)
            return;

        try
        {
            Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
            _mediaPlayer.Playing += OnPlaybackStateChanged;
            _mediaPlayer.Paused += OnPlaybackStateChanged;
            _mediaPlayer.Stopped += OnPlaybackStateChanged;
            _mediaPlayer.TimeChanged += OnTimeChanged;
            _mediaPlayer.EndReached += OnEndReached;
            _initializationError = null;
        }
        catch (Exception ex)
        {
            _initializationError = $"Не удалось запустить LibVLC: {ex.Message}";
            _mediaPlayer = null;
            _libVlc = null;
        }
    }

    private void DisposeMedia()
    {
        _media?.Dispose();
        _media = null;
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
        TrackEnded?.Invoke(this, EventArgs.Empty);
    }
}
