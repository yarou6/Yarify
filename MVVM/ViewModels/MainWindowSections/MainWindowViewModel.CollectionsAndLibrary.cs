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
    private async Task LoadLikedAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetLikedTracksAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка лайков: {error}"; return; }
        await HydrateAlbumTitlesAsync(items);

        LikedTracks.Clear();
        _likedSongIds.Clear();
        foreach (var item in items) { LikedTracks.Add(item); _likedSongIds.Add(item.Id); }
        SelectedLikedTrack = LikedTracks.FirstOrDefault();
        OnPropertyChanged(nameof(LikedTracksCount));
        OnPropertyChanged(nameof(LikedOwnerName));
        OnPropertyChanged(nameof(LikedHeaderStats));
        OnPropertyChanged(nameof(LikeButtonText));
        OnPropertyChanged(nameof(CanMoveQueueUp));
        OnPropertyChanged(nameof(CanMoveQueueDown));
        await LoadHomeLibraryHighlightsAsync();
    }

    private async Task LoadQueueAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetQueueAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка очереди: {error}"; return; }

        var selectedQueueId = SelectedQueueItem?.QueueId;
        QueueItems.Clear();
        foreach (var item in items) QueueItems.Add(item);

        if (QueueItems.Count == 0)
        {
            SelectedQueueItem = null;
        }
        else
        {
            SelectedQueueItem = selectedQueueId is null
                ? QueueItems[0]
                : QueueItems.FirstOrDefault(x => x.QueueId == selectedQueueId.Value) ?? QueueItems[0];
        }

        OnPropertyChanged(nameof(CanMoveQueueUp));
        OnPropertyChanged(nameof(CanMoveQueueDown));
        OnPropertyChanged(nameof(UpcomingQueueItems));
        UpdateNowPlayingPreview();
    }

    private async Task LoadPlaylistsAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetPlaylistsAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка плейлистов: {error}"; return; }

        Playlists.Clear();
        foreach (var item in items) Playlists.Add(item);
        if (Playlists.Count == 0)
        {
            SelectedPlaylist = null;
            PlaylistTitleText = "Плейлист";
            PlaylistMetaText = "Выбери или создай плейлист";
            PlaylistCoverPath = string.Empty;
            _playlistCoverBitmap = null;
            OnPropertyChanged(nameof(PlaylistCoverImage));
        }
        else
        {
            if (SelectedPlaylist is null)
                SelectedPlaylist = Playlists[0];
            else
                SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == SelectedPlaylist.Id) ?? Playlists[0];

            UpdatePlaylistHeaderFromSelection();
        }

        await BuildSearchResultsAsync();
        OnPropertyChanged(nameof(PublicPlaylistsCount));
        OnPropertyChanged(nameof(ProfileStatsText));
        await LoadHomeLibraryHighlightsAsync();
    }

    private async Task LoadPlaylistTracksAsync()
    {
        PlaylistTracks.Clear();
        if (SelectedPlaylist is null) return;

        var (items, error) = await _authSessionService.ApiClient.GetPlaylistTracksAsync(SelectedPlaylist.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка треков плейлиста: {error}"; return; }
        await HydrateAlbumTitlesAsync(items);

        var order = 1;
        foreach (var item in items)
        {
            item.TrackOrder = order++;
            PlaylistTracks.Add(item);
        }
        SelectedPlaylistTrack = PlaylistTracks.FirstOrDefault();

        UpdatePlaylistHeaderFromSelection();
    }

    private async Task HydrateAlbumTitlesAsync(IEnumerable<TrackListItemDto> tracks)
    {
        var list = tracks.ToList();
        foreach (var track in list)
        {
            if (track.AlbumId is null || track.AlbumId.Value <= 0)
                continue;

            if (!string.IsNullOrWhiteSpace(track.AlbumTitle))
            {
                _albumTitleCache[track.AlbumId.Value] = track.AlbumTitle!;
                continue;
            }

            if (_albumTitleCache.TryGetValue(track.AlbumId.Value, out var cachedTitle) && !string.IsNullOrWhiteSpace(cachedTitle))
            {
                track.AlbumTitle = cachedTitle;
                continue;
            }

            var (album, error) = await _authSessionService.ApiClient.GetAlbumAsync(track.AlbumId.Value);
            if (!string.IsNullOrWhiteSpace(error) || album is null || string.IsNullOrWhiteSpace(album.Title))
                continue;

            _albumTitleCache[track.AlbumId.Value] = album.Title;
            track.AlbumTitle = album.Title;
        }
    }

    private async Task LikeSelectedTrackAsync()
    {
        if (SelectedTrack is null) return;
        var error = _likedSongIds.Contains(SelectedTrack.Id)
            ? await _authSessionService.ApiClient.UnlikeTrackAsync(SelectedTrack.Id)
            : await _authSessionService.ApiClient.LikeTrackAsync(SelectedTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка лайка: {error}"; return; }
        await LoadLikedAsync();
    }

    private async Task AddSelectedToQueueAsync()
    {
        if (SelectedTrack is null) return;
        var error = await _authSessionService.ApiClient.AddToQueueAsync(SelectedTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка очереди: {error}"; return; }
        await LoadQueueAsync();
    }

    public async Task AddTrackToQueueNextAsync(TrackListItemDto track)
    {
        var error = await _authSessionService.ApiClient.AddToQueueNextAsync(track.Id);
        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка очереди: {error}";
            return;
        }

        await LoadQueueAsync();
        Status = $"Трек \"{track.Title}\" будет проигран следующим.";
    }

    private async Task RemoveSelectedQueueAsync()
    {
        if (SelectedQueueItem is null) return;
        var error = await _authSessionService.ApiClient.RemoveFromQueueAsync(SelectedQueueItem.QueueId);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка удаления из очереди: {error}"; return; }
        await LoadQueueAsync();
    }

    private async Task ClearQueueAsync()
    {
        var error = await _authSessionService.ApiClient.ClearQueueAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка очистки очереди: {error}"; return; }
        await LoadQueueAsync();
    }

    private async Task MoveSelectedQueueUpAsync()
    {
        if (SelectedQueueItem is null) return;
        var error = await _authSessionService.ApiClient.MoveQueueUpAsync(SelectedQueueItem.QueueId);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка перемещения вверх: {error}"; return; }
        await LoadQueueAsync();
    }

    private async Task MoveSelectedQueueDownAsync()
    {
        if (SelectedQueueItem is null) return;
        var error = await _authSessionService.ApiClient.MoveQueueDownAsync(SelectedQueueItem.QueueId);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка перемещения вниз: {error}"; return; }
        await LoadQueueAsync();
    }

    private async Task CreatePlaylistAsync()
    {
        var localCoverPath = IsExistingLocalFile(NewPlaylistCoverPath) ? NewPlaylistCoverPath.Trim() : null;
        var coverPath = string.IsNullOrWhiteSpace(localCoverPath) && !string.IsNullOrWhiteSpace(NewPlaylistCoverPath)
            ? NewPlaylistCoverPath.Trim()
            : null;

        var (playlist, error) = await _authSessionService.ApiClient.CreatePlaylistAsync(new CreatePlaylistRequestDto
        {
            Title = NewPlaylistTitle.Trim(),
            Description = string.IsNullOrWhiteSpace(NewPlaylistDescription) ? null : NewPlaylistDescription.Trim(),
            CoverPath = coverPath,
            IsPublic = NewPlaylistIsPublic
        });

        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка создания плейлиста: {error}"; return; }

        if (playlist is not null && !string.IsNullOrWhiteSpace(localCoverPath))
        {
            var uploadError = await _authSessionService.ApiClient.UploadPlaylistCoverAsync(playlist.Id, localCoverPath);
            if (!string.IsNullOrWhiteSpace(uploadError))
            {
                Status = $"Плейлист создан, но обложка не загружена: {uploadError}";
                return;
            }
        }

        NewPlaylistTitle = string.Empty;
        NewPlaylistDescription = string.Empty;
        NewPlaylistCoverPath = string.Empty;
        NewPlaylistIsPublic = false;
        await LoadPlaylistsAsync();
        if (playlist is not null) SelectedPlaylist = Playlists.FirstOrDefault(x => x.Id == playlist.Id);
    }

    private void OpenCreatePlaylistModal()
    {
        IsPlaylistEditMode = false;
        NewPlaylistTitle = string.Empty;
        NewPlaylistDescription = string.Empty;
        NewPlaylistCoverPath = string.Empty;
        NewPlaylistIsPublic = false;
        IsPlaylistModalOpen = true;
        OnPropertyChanged(nameof(PlaylistModalHeader));
        OnPropertyChanged(nameof(PlaylistSubmitText));
    }

    private void OpenEditPlaylistModal()
    {
        if (SelectedPlaylist is null) return;
        IsPlaylistEditMode = true;
        NewPlaylistTitle = SelectedPlaylist.Title;
        NewPlaylistDescription = SelectedPlaylist.Description ?? string.Empty;
        NewPlaylistCoverPath = SelectedPlaylist.CoverPath ?? string.Empty;
        NewPlaylistIsPublic = SelectedPlaylist.IsPublic;
        IsPlaylistModalOpen = true;
        OnPropertyChanged(nameof(PlaylistModalHeader));
        OnPropertyChanged(nameof(PlaylistSubmitText));
    }

    private async Task SavePlaylistModalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPlaylistTitle)) return;

        if (IsPlaylistEditMode)
        {
            if (SelectedPlaylist is null) return;

            var localCoverPath = IsExistingLocalFile(NewPlaylistCoverPath) ? NewPlaylistCoverPath.Trim() : null;
            var coverPath = string.IsNullOrWhiteSpace(localCoverPath) && !string.IsNullOrWhiteSpace(NewPlaylistCoverPath)
                ? NewPlaylistCoverPath.Trim()
                : (string.IsNullOrWhiteSpace(NewPlaylistCoverPath) ? null : SelectedPlaylist.CoverPath);

            var (playlist, updateError) = await _authSessionService.ApiClient.UpdatePlaylistAsync(SelectedPlaylist.Id, new UpdatePlaylistRequestDto
            {
                Title = NewPlaylistTitle.Trim(),
                Description = string.IsNullOrWhiteSpace(NewPlaylistDescription) ? null : NewPlaylistDescription.Trim(),
                CoverPath = coverPath,
                IsPublic = NewPlaylistIsPublic
            });

            if (!string.IsNullOrWhiteSpace(updateError)) { Status = $"Ошибка редактирования плейлиста: {updateError}"; return; }

            if (playlist is not null && !string.IsNullOrWhiteSpace(localCoverPath))
            {
                var uploadError = await _authSessionService.ApiClient.UploadPlaylistCoverAsync(playlist.Id, localCoverPath);
                if (!string.IsNullOrWhiteSpace(uploadError))
                {
                    Status = $"Плейлист обновлен, но обложка не загружена: {uploadError}";
                    return;
                }
            }

            await LoadPlaylistsAsync();
            if (playlist is not null) SelectedPlaylist = Playlists.FirstOrDefault(x => x.Id == playlist.Id) ?? SelectedPlaylist;
        }
        else
        {
            await CreatePlaylistAsync();
        }

        IsPlaylistModalOpen = false;
        NewPlaylistCoverPath = string.Empty;
    }

    public async Task AddTrackToPlaylistByIdsAsync(int songId, int playlistId)
    {
        var error = await _authSessionService.ApiClient.AddTrackToPlaylistAsync(playlistId, songId);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка drag-and-drop: {error}"; return; }

        await LoadPlaylistsAsync();
        if (SelectedPlaylist?.Id == playlistId)
            await LoadPlaylistTracksAsync();
        Status = "Трек добавлен в плейлист.";
    }

    public async Task AddCurrentTrackToPlaylistAsync(int playlistId)
    {
        if (CurrentTrack is null)
            return;

        var playlist = Playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null)
            return;

        await AddTrackToPlaylistByIdsAsync(CurrentTrack.Id, playlistId);
        SelectedPlaylist = playlist;
        Status = $"Трек \"{CurrentTrack.Title}\" добавлен в \"{playlist.Title}\".";
    }

    private async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null) return;
        var error = await _authSessionService.ApiClient.DeletePlaylistAsync(SelectedPlaylist.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка удаления плейлиста: {error}"; return; }
        await LoadPlaylistsAsync();
        _isInitializing = false;
    }

    private async Task AddSelectedTrackToPlaylistAsync()
    {
        if (SelectedTrack is null || SelectedPlaylist is null) return;
        var error = await _authSessionService.ApiClient.AddTrackToPlaylistAsync(SelectedPlaylist.Id, SelectedTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка добавления в плейлист: {error}"; return; }
        await LoadPlaylistsAsync();
        await LoadPlaylistTracksAsync();
    }

    private async Task AddCurrentTrackToLikedAsync()
    {
        if (CurrentTrack is null)
            return;

        var error = _likedSongIds.Contains(CurrentTrack.Id)
            ? await _authSessionService.ApiClient.UnlikeTrackAsync(CurrentTrack.Id)
            : await _authSessionService.ApiClient.LikeTrackAsync(CurrentTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка лайка: {error}"; return; }
        await LoadLikedAsync();
    }

    private async Task AddCurrentTrackToPlaylistAsync()
    {
        if (CurrentTrack is null)
            return;

        var playlist = SelectedPlaylist ?? Playlists.FirstOrDefault();
        if (playlist is null)
        {
            Status = "Сначала создай хотя бы один плейлист.";
            return;
        }

        await AddCurrentTrackToPlaylistAsync(playlist.Id);
    }

    private async Task RemoveSelectedPlaylistTrackAsync()
    {
        if (SelectedPlaylistTrack is null || SelectedPlaylist is null) return;
        var error = await _authSessionService.ApiClient.RemoveTrackFromPlaylistAsync(SelectedPlaylist.Id, SelectedPlaylistTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка удаления из плейлиста: {error}"; return; }
        await LoadPlaylistsAsync();
        await LoadPlaylistTracksAsync();
    }

    private async Task OpenSelectedArtistAsync()
    {
        var src = SelectedTrack ?? CurrentTrack;
        if (src is null) return;
        await OpenArtistByIdAsync(src.ArtistUserId);
    }

    private async Task OpenAlbumArtistAsync()
    {
        var artistUserId = SelectedAlbumTrack?.ArtistUserId;
        if (artistUserId is null or <= 0)
            artistUserId = _currentArtistUserId;

        if (artistUserId <= 0)
        {
            Status = "Не удалось определить артиста альбома.";
            return;
        }

        await OpenArtistByIdAsync(artistUserId.Value);
    }

    private async Task OpenSelectedAlbumAsync()
    {
        var albumId = SelectedTrack?.AlbumId ?? CurrentTrack?.AlbumId;
        if (albumId is null) return;
        await OpenAlbumByIdAsync(albumId.Value);
    }

    private async Task OpenSelectedArtistAlbumAsync()
    {
        if (SelectedArtistAlbum is null) return;
        await OpenAlbumByIdAsync(SelectedArtistAlbum.Id);
    }

    private async Task OpenAlbumByIdAsync(int albumId)
    {
        var (album, error) = await _authSessionService.ApiClient.GetAlbumAsync(albumId);
        if (!string.IsNullOrWhiteSpace(error) || album is null) { Status = $"Ошибка альбома: {error}"; return; }

        AlbumHeader = $"{album.Title} - {album.ArtistName}";
        AlbumTitleText = album.Title;
        AlbumArtistNameText = album.ArtistName;
        AlbumCoverPath = album.CoverPath ?? string.Empty;
        _albumCoverBitmap = album.CoverBitmap;
        OnPropertyChanged(nameof(AlbumCoverImage));
        AlbumTracks.Clear();
        var order = 1;
        foreach (var t in album.Tracks)
        {
            t.TrackOrder = order++;
            AlbumTracks.Add(t);
        }
        _currentArtistUserId = album.Tracks.FirstOrDefault()?.ArtistUserId ?? 0;
        SelectedAlbumTrack = AlbumTracks.FirstOrDefault();
        var totalDuration = TimeSpan.FromSeconds(Math.Max(0, album.Tracks.Sum(t => t.DurationSec)));
        var minutesPart = totalDuration.Hours > 0
            ? $"{(int)totalDuration.TotalHours} ч. {totalDuration.Minutes} мин."
            : $"{totalDuration.Minutes} мин. {totalDuration.Seconds} сек.";
        AlbumMetaText = $"{album.Tracks.Count} треков, {minutesPart}";
        OnPropertyChanged(nameof(AlbumTotalPlays));
        OnPropertyChanged(nameof(AlbumTotalPlaysText));
        ActiveSection = "album";
        RaiseCanExecutes();
    }

    private void UpdatePlaylistHeaderFromSelection()
    {
        if (SelectedPlaylist is null)
        {
            PlaylistTitleText = "Плейлист";
            PlaylistMetaText = "Выбери или создай плейлист";
            PlaylistCoverPath = string.Empty;
            _playlistCoverBitmap = null;
            OnPropertyChanged(nameof(PlaylistCoverImage));
            return;
        }

        PlaylistTitleText = SelectedPlaylist.Title;
        PlaylistMetaText = $"{Math.Max(0, PlaylistTracks.Count)} треков";
        PlaylistCoverPath = SelectedPlaylist.CoverPath ?? string.Empty;
        _playlistCoverBitmap = SelectedPlaylist.CoverBitmap;
        OnPropertyChanged(nameof(PlaylistCoverImage));
    }

    private async Task LoadHomeLibraryHighlightsAsync()
    {
        var (history, error) = await _authSessionService.ApiClient.GetRecentHistoryAsync(120);
        if (!string.IsNullOrWhiteSpace(error))
            return;

        var likedRecentTracks = history
            .Where(h => h.Track is not null && _likedSongIds.Contains(h.Track.Id))
            .Select(h => h.Track)
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .Take(6)
            .ToList();

        HomeLikedRecentTracks.Clear();
        foreach (var track in likedRecentTracks)
            HomeLikedRecentTracks.Add(track);

        var collections = new List<HomeMediaCollectionItemDto>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in history)
        {
            var collection = TryBuildHomeCollectionFromHistory(item);
            if (collection is null)
                continue;

            var key = $"{collection.Kind}:{collection.Id}";
            if (!keys.Add(key))
                continue;

            collections.Add(collection);
            if (collections.Count >= 6)
                break;
        }

        if (collections.Count < 6)
        {
            foreach (var track in RecentTracks.Where(t => t.AlbumId.HasValue))
            {
                var albumId = track.AlbumId!.Value;
                var key = $"album:{albumId}";
                if (!keys.Add(key))
                    continue;

                collections.Add(new HomeMediaCollectionItemDto
                {
                    Kind = "album",
                    Id = albumId,
                    Title = string.IsNullOrWhiteSpace(track.AlbumTitle) ? $"Альбом #{albumId}" : track.AlbumTitle!,
                    Subtitle = "Недавно прослушано",
                    CoverPath = track.CoverPath,
                    CoverBitmap = track.CoverBitmap
                });

                if (collections.Count >= 6)
                    break;
            }
        }

        HomeRecentCollections.Clear();
        foreach (var collection in collections.Take(6))
            HomeRecentCollections.Add(collection);
    }

    private HomeMediaCollectionItemDto? TryBuildHomeCollectionFromHistory(ListeningHistoryItemDto item)
    {
        if (item.SourceId is null or <= 0)
            return null;

        if (string.Equals(item.SourceType, "Playlist", StringComparison.OrdinalIgnoreCase))
        {
            var playlist = Playlists.FirstOrDefault(p => p.Id == item.SourceId.Value);
            return new HomeMediaCollectionItemDto
            {
                Kind = "playlist",
                Id = item.SourceId.Value,
                Title = playlist?.Title ?? $"Плейлист #{item.SourceId.Value}",
                Subtitle = playlist?.Subtitle ?? "Плейлист",
                CoverPath = playlist?.CoverPath ?? item.Track?.CoverPath,
                CoverBitmap = playlist?.CoverBitmap ?? item.Track?.CoverBitmap
            };
        }

        if (string.Equals(item.SourceType, "Album", StringComparison.OrdinalIgnoreCase))
        {
            var album = SearchResultAlbums.FirstOrDefault(a => a.Id == item.SourceId.Value);
            return new HomeMediaCollectionItemDto
            {
                Kind = "album",
                Id = item.SourceId.Value,
                Title = album?.Title
                    ?? (!string.IsNullOrWhiteSpace(item.Track?.AlbumTitle) ? item.Track.AlbumTitle! : $"Альбом #{item.SourceId.Value}"),
                Subtitle = "Альбом",
                CoverPath = album?.CoverPath ?? item.Track?.CoverPath,
                CoverBitmap = album?.CoverBitmap ?? item.Track?.CoverBitmap
            };
        }

        return null;
    }

    public async Task OpenHomeCollectionAsync(HomeMediaCollectionItemDto collection)
    {
        if (string.Equals(collection.Kind, "playlist", StringComparison.OrdinalIgnoreCase))
        {
            var playlist = Playlists.FirstOrDefault(p => p.Id == collection.Id);
            if (playlist is null)
            {
                Status = "Этот плейлист недоступен в твоей медиатеке.";
                return;
            }

            SelectedPlaylist = playlist;
            ActiveSection = "playlists";
            return;
        }

        await OpenAlbumByIdAsync(collection.Id);
    }
}




