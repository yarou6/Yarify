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
    }

    private async Task LoadPlaylistsAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetPlaylistsAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка плейлистов: {error}"; return; }

        Playlists.Clear();
        foreach (var item in items) Playlists.Add(item);
        if (Playlists.Count > 0 && SelectedPlaylist is null) SelectedPlaylist = Playlists[0];
        await BuildSearchResultsAsync();
        OnPropertyChanged(nameof(PublicPlaylistsCount));
        OnPropertyChanged(nameof(ProfileStatsText));
    }

    private async Task LoadPlaylistTracksAsync()
    {
        PlaylistTracks.Clear();
        if (SelectedPlaylist is null) return;

        var (items, error) = await _authSessionService.ApiClient.GetPlaylistTracksAsync(SelectedPlaylist.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка треков плейлиста: {error}"; return; }
        await HydrateAlbumTitlesAsync(items);

        foreach (var item in items) PlaylistTracks.Add(item);
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
        var (playlist, error) = await _authSessionService.ApiClient.CreatePlaylistAsync(new CreatePlaylistRequestDto
        {
            Title = NewPlaylistTitle.Trim(),
            Description = string.IsNullOrWhiteSpace(NewPlaylistDescription) ? null : NewPlaylistDescription.Trim(),
            IsPublic = NewPlaylistIsPublic
        });

        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка создания плейлиста: {error}"; return; }

        NewPlaylistTitle = string.Empty;
        NewPlaylistDescription = string.Empty;
        NewPlaylistIsPublic = false;
        await LoadPlaylistsAsync();
        if (playlist is not null) SelectedPlaylist = Playlists.FirstOrDefault(x => x.Id == playlist.Id);
    }

    private void OpenCreatePlaylistModal()
    {
        IsPlaylistEditMode = false;
        NewPlaylistTitle = string.Empty;
        NewPlaylistDescription = string.Empty;
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

            var (playlist, updateError) = await _authSessionService.ApiClient.UpdatePlaylistAsync(SelectedPlaylist.Id, new UpdatePlaylistRequestDto
            {
                Title = NewPlaylistTitle.Trim(),
                Description = string.IsNullOrWhiteSpace(NewPlaylistDescription) ? null : NewPlaylistDescription.Trim(),
                IsPublic = NewPlaylistIsPublic
            });

            if (!string.IsNullOrWhiteSpace(updateError)) { Status = $"Ошибка редактирования плейлиста: {updateError}"; return; }
            await LoadPlaylistsAsync();
            if (playlist is not null) SelectedPlaylist = Playlists.FirstOrDefault(x => x.Id == playlist.Id) ?? SelectedPlaylist;
        }
        else
        {
            await CreatePlaylistAsync();
        }

        IsPlaylistModalOpen = false;
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
        if (CurrentTrack is null || SelectedPlaylist is null)
            return;

        var error = await _authSessionService.ApiClient.AddTrackToPlaylistAsync(SelectedPlaylist.Id, CurrentTrack.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка добавления в плейлист: {error}"; return; }
        await LoadPlaylistTracksAsync();
        Status = $"Трек \"{CurrentTrack.Title}\" добавлен в \"{SelectedPlaylist.Title}\".";
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
}




