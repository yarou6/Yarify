using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MVVM.Models.Auth;
using MVVM.Models.Library;
using MVVM.Models.Playback;
using MVVM.Models.Profile;
using MVVM.Models.Subscriptions;
using MVVM.Services;
using MVVM.Tools;
using TagLibFile = TagLib.File;

namespace MVVM.ViewModels;

public partial class MainWindowViewModel
{
    public async Task PlayTrackFromUiAsync(TrackListItemDto track)
    {
        var uiContext = ResolvePlaybackContextForUiTrack(track);
        if (!string.IsNullOrWhiteSpace(uiContext))
            _playbackContextKey = uiContext;

        SelectedTrack = track;
        if (AlbumTracks.Contains(track))
            SelectedAlbumTrack = track;
        await PlayTrackAsync(track);
    }

    private string ResolvePlaybackContextForUiTrack(TrackListItemDto track)
    {
        if (IsTracksSection)
        {
            if (ForYouTracks.Any(t => t.Id == track.Id))
                return "for-you";
            if (RecentTracks.Any(t => t.Id == track.Id))
                return "recent";
            if (HomeLikedRecentTracks.Any(t => t.Id == track.Id))
                return "home-liked";
        }

        if (IsAlbumSection && AlbumTracks.Any(t => t.Id == track.Id))
            return "album";
        if (IsPlaylistsSection && PlaylistTracks.Any(t => t.Id == track.Id))
            return "playlist";
        if (IsLikedSection && LikedTracks.Any(t => t.Id == track.Id))
            return "liked";
        if (IsArtistSection && ArtistTopTracks.Any(t => t.Id == track.Id))
            return "artist";
        if (IsSearchSection && SearchResultTracks.Any(t => t.Id == track.Id))
            return "search";
        if (IsTracksSection && Tracks.Any(t => t.Id == track.Id))
            return "tracks";

        if (PlaylistTracks.Any(t => t.Id == track.Id))
            return "playlist";
        if (LikedTracks.Any(t => t.Id == track.Id))
            return "liked";
        if (ArtistTopTracks.Any(t => t.Id == track.Id))
            return "artist";
        if (SearchResultTracks.Any(t => t.Id == track.Id))
            return "search";
        if (ForYouTracks.Any(t => t.Id == track.Id))
            return "for-you";
        if (RecentTracks.Any(t => t.Id == track.Id))
            return "recent";
        if (HomeLikedRecentTracks.Any(t => t.Id == track.Id))
            return "home-liked";
        if (AlbumTracks.Any(t => t.Id == track.Id))
            return "album";
        if (Tracks.Any(t => t.Id == track.Id))
            return "tracks";

        return _playbackContextKey;
    }

    public async Task OpenAlbumByIdFromUiAsync(int albumId)
    {
        await OpenAlbumByIdAsync(albumId);
    }

    public async Task OpenArtistByIdFromUiAsync(int artistUserId)
    {
        await OpenArtistByIdAsync(artistUserId);
    }

    public async Task OpenTrackAlbumFromUiAsync(TrackListItemDto? track)
    {
        if (track?.AlbumId is null or <= 0)
        {
            Status = "Страница сингла будет добавлена позже.";
            return;
        }

        await OpenAlbumByIdAsync(track.AlbumId.Value);
    }

    public async Task OpenArtistReleaseFromUiAsync(ArtistReleaseItemDto release)
    {
        if (release is null)
            return;

        if (release.IsAlbum && release.AlbumId.HasValue)
        {
            await OpenAlbumByIdAsync(release.AlbumId.Value);
            return;
        }

        if (release.TrackId.HasValue)
        {
            var track = ArtistTopTracks.FirstOrDefault(t => t.Id == release.TrackId.Value)
                ?? Tracks.FirstOrDefault(t => t.Id == release.TrackId.Value);
            if (track is not null)
            {
                _playbackContextKey = ArtistTopTracks.Any(t => t.Id == track.Id) ? "artist" : "tracks";
                await PlayTrackAsync(track);
            }
        }
    }

    private Task PlayFromTracksAsync(TrackListItemDto? track)
    {
        _playbackContextKey = "tracks";
        return PlayTrackAsync(track);
    }

    private Task PlayFromLikedAsync(TrackListItemDto? track)
    {
        _playbackContextKey = "liked";
        return PlayTrackAsync(track);
    }

    private Task PlayFromQueueAsync(TrackListItemDto? track)
    {
        if (track is not null)
            _playbackContextKey = ResolvePlaybackContextForUiTrack(track);
        return PlayTrackAsync(track);
    }

    private Task PlayFromPlaylistAsync(TrackListItemDto? track)
    {
        _playbackContextKey = "playlist";
        return PlayTrackAsync(track);
    }

    private Task PlayFromArtistAsync(TrackListItemDto? track)
    {
        _playbackContextKey = "artist";
        return PlayTrackAsync(track);
    }

    private TrackListItemDto? GetUpcomingTrackPreview()
    {
        if (PlaybackMode == PlaybackMode.RepeatOne)
            return CurrentTrack;
        var upcoming = BuildUpcomingQueueItems();
        return upcoming.FirstOrDefault()?.Track ?? CurrentTrack;
    }

    private IReadOnlyList<QueueItemDto> BuildUpcomingQueueItems()
    {
        if (PlaybackMode == PlaybackMode.RepeatOne)
            return Array.Empty<QueueItemDto>();

        var result = new List<QueueItemDto>();
        var seenTrackIds = new HashSet<int>();
        var position = 1;

        foreach (var queued in QueueItems.OrderBy(q => q.Position))
        {
            if (queued.Track is null)
                continue;

            if (!seenTrackIds.Add(queued.Track.Id))
                continue;

            result.Add(new QueueItemDto
            {
                QueueId = queued.QueueId,
                Position = position++,
                Track = queued.Track
            });
        }

        var activeList = GetActivePlaybackList();
        if (activeList.Count == 0)
            return result;
        if (CurrentTrack is null)
            return result;

        if (IsShuffleEnabled)
        {
            EnsureShuffleOrder();
            var shuffled = GetShuffledUpcomingTracks(activeList);

            foreach (var track in shuffled)
            {
                if (!seenTrackIds.Add(track.Id))
                    continue;

                result.Add(new QueueItemDto
                {
                    QueueId = 0,
                    Position = position++,
                    Track = track
                });
            }

            return result;
        }

        var currentIndex = CurrentTrack is null ? -1 : IndexOfTrackById(activeList, CurrentTrack.Id);
        var startIndex = currentIndex < 0 ? 0 : currentIndex + 1;

        for (var i = startIndex; i < activeList.Count; i++)
        {
            var track = activeList[i];
            if (!seenTrackIds.Add(track.Id))
                continue;

            result.Add(new QueueItemDto
            {
                QueueId = 0,
                Position = position++,
                Track = track
            });
        }

        if (PlaybackMode == PlaybackMode.RepeatAll && currentIndex >= 0)
        {
            for (var i = 0; i <= currentIndex; i++)
            {
                var track = activeList[i];
                if (!seenTrackIds.Add(track.Id))
                    continue;

                result.Add(new QueueItemDto
                {
                    QueueId = 0,
                    Position = position++,
                    Track = track
                });
            }
        }

        return result;
    }

    private void UpdateNowPlayingPreview()
    {
        OnPropertyChanged(nameof(NextTrackPreview));
        OnPropertyChanged(nameof(NextTrackTitle));
        OnPropertyChanged(nameof(NextTrackArtist));
        OnPropertyChanged(nameof(NextTrackCoverImage));
        OnPropertyChanged(nameof(UpcomingQueueItems));
    }

    private async Task HandleTrackEndedAsync()
    {
        if (_isAdvancingTrack)
            return;

        _isAdvancingTrack = true;
        try
        {
            await CompleteActiveListeningEventAsync(forceCompleted: true);
            await PlayNextTrackAsync();
        }
        finally
        {
            _isAdvancingTrack = false;
        }
    }

    private IReadOnlyList<TrackListItemDto> GetActivePlaybackList()
    {
        IReadOnlyList<TrackListItemDto>? byContext = _playbackContextKey switch
        {
            "album" => AlbumTracks,
            "playlist" => PlaylistTracks,
            "liked" => LikedTracks,
            "artist" => ArtistTopTracks,
            "search" => SearchResultTracks,
            "for-you" => ForYouTracks,
            "recent" => RecentTracks,
            "home-liked" => HomeLikedRecentTracks,
            "queue" => QueueItems.Select(q => q.Track).Where(t => t is not null).Cast<TrackListItemDto>().ToList(),
            _ => Tracks
        };

        if (byContext is not null && byContext.Count > 0)
            return byContext;

        return Tracks;
    }

    private void EnsurePlaybackContextForTrack(TrackListItemDto track)
    {
        var active = GetActivePlaybackList();
        if (active.Any(t => t.Id == track.Id))
            return;

        if (AlbumTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "album";
        else if (PlaylistTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "playlist";
        else if (LikedTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "liked";
        else if (ArtistTopTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "artist";
        else if (SearchResultTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "search";
        else if (ForYouTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "for-you";
        else if (RecentTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "recent";
        else if (HomeLikedRecentTracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "home-liked";
        else if (Tracks.Any(t => t.Id == track.Id))
            _playbackContextKey = "tracks";
    }

    private static int IndexOfTrackById(IReadOnlyList<TrackListItemDto> list, int trackId)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == trackId)
                return i;
        }

        return -1;
    }

    private async Task StartActiveListeningEventAsync(TrackListItemDto track)
    {
        var (sourceType, sourceId) = ResolveListeningSource(track);
        var (eventId, error) = await _authSessionService.ApiClient.StartListeningEventAsync(track.Id, sourceType, sourceId, DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(error) || eventId is null)
        {
            _activeListeningEventId = null;
            _activeListeningSongId = 0;
            return;
        }

        _activeListeningEventId = eventId;
        _activeListeningSongId = track.Id;
        _lastListeningProgressSentAt = DateTime.MinValue;
    }

    private async Task ReportListeningProgressAsync()
    {
        if (_activeListeningEventId is null || CurrentTrack is null || CurrentTrack.Id != _activeListeningSongId)
            return;

        if (!_audioPlayer.IsPlaying)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastListeningProgressSentAt).TotalSeconds < 2)
            return;

        _lastListeningProgressSentAt = now;
        var playedMs = (int)Math.Clamp(Math.Round(PositionSeconds * 1000d), 0d, int.MaxValue);
        await _authSessionService.ApiClient.ReportListeningProgressAsync(_activeListeningEventId.Value, playedMs, null);
    }

    private async Task CompleteActiveListeningEventAsync(bool forceCompleted)
    {
        if (_activeListeningEventId is null || CurrentTrack is null || CurrentTrack.Id != _activeListeningSongId)
            return;

        var playedMs = (int)Math.Clamp(Math.Round(PositionSeconds * 1000d), 0d, int.MaxValue);
        if (forceCompleted && CurrentTrack.DurationSec > 0)
            playedMs = Math.Max(playedMs, CurrentTrack.DurationSec * 1000);

        var completed = CurrentTrack.DurationSec > 0 && playedMs >= CurrentTrack.DurationSec * 1000;
        await _authSessionService.ApiClient.CompleteListeningEventAsync(_activeListeningEventId.Value, playedMs, completed, DateTime.UtcNow);
        _activeListeningEventId = null;
        _activeListeningSongId = 0;
        _lastListeningProgressSentAt = DateTime.MinValue;
    }

    private async Task PlayTrackAsync(TrackListItemDto? track)
    {
        if (track is null) return;
        if (!AllowExplicitContent && track.Explicit)
        {
            Status = "Этот трек доступен только после подтверждения 18+ в настройках.";
            return;
        }
        if (string.IsNullOrWhiteSpace(track.Source)) { Status = "У трека нет Source."; return; }
        EnsurePlaybackContextForTrack(track);

        try
        {
            await CompleteActiveListeningEventAsync(forceCompleted: false);
            _audioPlayer.Load(track.Source);
            _audioPlayer.Volume = IsMuted ? 0d : VolumePercent / 100d;
            _audioPlayer.Play();
            CurrentTrack = track;
            UpdateShuffleCursorForTrack(track);
            UpdateCurrentArtistPlaysTotal(track.ArtistUserId);
            SelectedTrack = track;
            var albumTrack = AlbumTracks.FirstOrDefault(t => t.Id == track.Id);
            if (albumTrack is not null)
            {
                SelectedAlbumTrack = albumTrack;
                _playbackContextKey = "album";
            }
            RememberTrack(track);
            Status = $"Сейчас играет: {track.Title}";
            UpdateNowPlayingPreview();
            UpdatePlayback();
            UpdateTime();
            await StartActiveListeningEventAsync(track);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Status = $"Ошибка воспроизведения: {ex.Message}";
        }
    }

    private void PlayPause()
    {
        if (CurrentTrack is null) return;
        if (_audioPlayer.IsPlaying) _audioPlayer.Pause(); else _audioPlayer.Play();
        UpdatePlayback();
    }

    private async Task PlayNextTrackAsync()
    {
        if (PlaybackMode == PlaybackMode.RepeatOne && CurrentTrack is not null) { await PlayTrackAsync(CurrentTrack); return; }
        if (QueueItems.Count > 0)
        {
            var q = QueueItems[0];
            await PlayTrackAsync(q.Track);
            await _authSessionService.ApiClient.RemoveFromQueueAsync(q.QueueId);
            await LoadQueueAsync();
            UpdateNowPlayingPreview();
            return;
        }

        var activeList = GetActivePlaybackList();
        if (activeList.Count == 0)
            return;

        TrackListItemDto? next = IsShuffleEnabled ? NextShuffled(activeList) : NextFromTracks(activeList);
        if (next is null)
        {
            _audioPlayer.Stop();
            Status = "Конец списка треков.";
            UpdateNowPlayingPreview();
            return;
        }

        await PlayTrackAsync(next);
    }

    private TrackListItemDto? NextShuffled(IReadOnlyList<TrackListItemDto> activeList)
    {
        EnsureShuffleOrder();
        if (activeList.Count == 0)
            return null;

        if (_shuffleTrackOrder.Count == 0)
            return null;

        if (CurrentTrack is null)
        {
            var firstId = _shuffleTrackOrder[0];
            _shuffleCursor = 0;
            return activeList.FirstOrDefault(t => t.Id == firstId);
        }

        var currentIndex = _shuffleTrackOrder.IndexOf(CurrentTrack.Id);
        if (currentIndex < 0)
        {
            EnsureShuffleOrder();
            currentIndex = _shuffleTrackOrder.IndexOf(CurrentTrack.Id);
            if (currentIndex < 0)
                currentIndex = -1;
        }

        var nextIndex = currentIndex + 1;
        if (nextIndex >= _shuffleTrackOrder.Count)
            nextIndex = 0;

        _shuffleCursor = nextIndex;
        var nextId = _shuffleTrackOrder[nextIndex];
        return activeList.FirstOrDefault(t => t.Id == nextId);
    }

    private TrackListItemDto? NextFromTracks(IReadOnlyList<TrackListItemDto> activeList)
    {
        if (activeList.Count == 0)
            return null;
        if (CurrentTrack is null)
            return activeList[0];

        var idx = IndexOfTrackById(activeList, CurrentTrack.Id);
        if (idx < 0)
            return activeList[0];

        idx++;
        if (idx >= activeList.Count)
            return PlaybackMode == PlaybackMode.RepeatAll ? activeList[0] : null;

        return activeList[idx];
    }

    private void PlayPreviousTrack()
    {
        var activeList = GetActivePlaybackList();
        if (activeList.Count == 0)
            return;
        if (CurrentTrack is null)
        {
            _ = PlayTrackAsync(activeList[0]);
            return;
        }

        var idx = IndexOfTrackById(activeList, CurrentTrack.Id);
        if (idx < 0)
        {
            _ = PlayTrackAsync(activeList[0]);
            return;
        }

        idx--;
        if (idx < 0)
            idx = PlaybackMode == PlaybackMode.RepeatAll ? activeList.Count - 1 : 0;
        _ = PlayTrackAsync(activeList[idx]);
        UpdateNowPlayingPreview();
    }

    private void ToggleRepeatMode() => PlaybackMode = PlaybackMode switch { PlaybackMode.Normal => PlaybackMode.RepeatAll, PlaybackMode.RepeatAll => PlaybackMode.RepeatOne, _ => PlaybackMode.Normal };

    private void EnsureShuffleOrder(bool forceReshuffle = false)
    {
        var activeList = GetActivePlaybackList();
        var activeIds = activeList.Select(t => t.Id).Distinct().ToList();
        if (activeIds.Count == 0)
        {
            _shuffleTrackOrder = new List<int>();
            _shuffleCursor = -1;
            return;
        }

        var requiresReset = _shuffleTrackOrder.Count != activeIds.Count ||
                            _shuffleTrackOrder.Except(activeIds).Any() ||
                            activeIds.Except(_shuffleTrackOrder).Any();

        if (requiresReset || forceReshuffle)
        {
            _shuffleTrackOrder = activeIds.ToList();
            for (var i = _shuffleTrackOrder.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_shuffleTrackOrder[i], _shuffleTrackOrder[j]) = (_shuffleTrackOrder[j], _shuffleTrackOrder[i]);
            }
        }

        if (CurrentTrack is null)
        {
            _shuffleCursor = -1;
            return;
        }

        _shuffleCursor = _shuffleTrackOrder.IndexOf(CurrentTrack.Id);
    }

    private void UpdateShuffleCursorForTrack(TrackListItemDto track)
    {
        if (!IsShuffleEnabled)
            return;

        EnsureShuffleOrder();
        var idx = _shuffleTrackOrder.IndexOf(track.Id);
        if (idx >= 0)
        {
            _shuffleCursor = idx;
            return;
        }

        _shuffleTrackOrder.Add(track.Id);
        _shuffleCursor = _shuffleTrackOrder.Count - 1;
    }

    private IReadOnlyList<TrackListItemDto> GetShuffledUpcomingTracks(IReadOnlyList<TrackListItemDto> activeList)
    {
        if (_shuffleTrackOrder.Count == 0)
            return Array.Empty<TrackListItemDto>();

        var byId = activeList.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        var currentIndex = CurrentTrack is null ? -1 : _shuffleTrackOrder.IndexOf(CurrentTrack.Id);
        var start = currentIndex < 0 ? 0 : currentIndex + 1;
        var result = new List<TrackListItemDto>();

        for (var i = start; i < _shuffleTrackOrder.Count; i++)
        {
            if (byId.TryGetValue(_shuffleTrackOrder[i], out var track))
                result.Add(track);
        }

        if (PlaybackMode == PlaybackMode.RepeatAll && currentIndex >= 0)
        {
            for (var i = 0; i <= currentIndex; i++)
            {
                if (byId.TryGetValue(_shuffleTrackOrder[i], out var track))
                    result.Add(track);
            }
        }

        return result;
    }

    private void UpdateCurrentArtistPlaysTotal(int artistUserId)
    {
        if (artistUserId <= 0)
            return;

        var all = Tracks
            .Concat(ArtistTopTracks)
            .Concat(AlbumTracks)
            .Concat(LikedTracks)
            .Concat(PlaylistTracks)
            .Concat(ForYouTracks)
            .Concat(RecentTracks)
            .GroupBy(t => t.Id)
            .Select(g => g.First());
        _currentArtistPlaysTotal = all.Where(t => t.ArtistUserId == artistUserId).Sum(t => Math.Max(0, t.PlayCount));
        if (_currentArtistPlaysTotal > 0 && artistUserId == _currentArtistUserId)
            _artistMonthlyStreams = _currentArtistPlaysTotal;
        OnPropertyChanged(nameof(CurrentArtistTotalStreamsText));
        OnPropertyChanged(nameof(ArtistMonthlyStreamsText));
    }

    private (string SourceType, int? SourceId) ResolveListeningSource(TrackListItemDto track)
    {
        if (_playbackContextKey == "playlist")
        {
            var playlistId = SelectedPlaylist?.Id;
            if (playlistId is not null && playlistId > 0)
                return ("Playlist", playlistId);
        }

        if (_playbackContextKey == "album" && track.AlbumId is > 0)
            return ("Album", track.AlbumId);

        if (track.AlbumId is > 0)
            return ("Album", track.AlbumId);

        return ("Direct", null);
    }

    private async Task PlayAlbumPrimaryAsync()
    {
        _playbackContextKey = "album";
        if (SelectedAlbumTrack is null)
            SelectedAlbumTrack = AlbumTracks.FirstOrDefault();

        if (SelectedAlbumTrack is null)
            return;

        if (CurrentTrack?.Id == SelectedAlbumTrack.Id)
        {
            PlayPause();
            return;
        }

        await PlayTrackAsync(SelectedAlbumTrack);
    }

    private bool CanSelectPlan(SubscriptionPlanDto? plan)
    {
        return !IsBusy && plan is not null && CurrentSubscription?.PlanId != plan.Id;
    }

    private async Task SelectPlanAsync(SubscriptionPlanDto? plan)
    {
        if (plan is null)
            return;

        var (updated, error) = await _authSessionService.ApiClient.ChangeMySubscriptionAsync(new ChangeSubscriptionRequestDto
        {
            PlanId = plan.Id,
            IsAutoRenew = !plan.IsFree
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка выбора плана: {error}";
            return;
        }

        if (updated is not null)
            CurrentSubscription = updated;

        Status = $"Выбран план: {plan.Title}.";
    }
}




