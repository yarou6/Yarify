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
    private void AddTrackAction()
    {
        if (!HasArtistName)
        {
            Status = "Сначала добавьте имя артиста в настройках.";
            ActiveSection = "settings";
            return; 
        }

        OpenAddTrackModal();
    }

    private void OpenAddTrackModal()
    {
        _draftAlbumId = null;
        _albumTracksRemaining = 0;
        _albumTracksTotal = 0;
        IsAlbumTrackMode = false;
        AddTrackAlbumTitle = string.Empty;
        AddTrackPlannedCountInput = "1";
        AddTrackTitleInput = string.Empty;
        AddTrackDurationInput = "180";
        AddTrackGenreSearchInput = string.Empty;
        AddTrackIsOnlineSource = false;
        AddTrackLocalPath = string.Empty;
        AddTrackStreamUrl = string.Empty;
        AddTrackAlbumCoverPath = string.Empty;
        AddTrackCoverPath = string.Empty;
        AddTrackExplicit = false;
        foreach (var genre in AddTrackGenres)
            genre.IsSelected = false;
        RefreshFilteredAddTrackGenres();
        OnPropertyChanged(nameof(AddTrackProgressText));
        IsAddTrackModalOpen = true;
    }

    private void CloseAddTrackModal()
    {
        IsAddTrackModalOpen = false;
        _draftAlbumId = null;
        _albumTracksRemaining = 0;
        _albumTracksTotal = 0;
        OnPropertyChanged(nameof(AddTrackProgressText));
    }

    private bool CanSubmitAddTrack()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(AddTrackTitleInput))
            return false;

        return int.TryParse(AddTrackDurationInput.Trim(), out var duration) && duration is >= 1 and <= 7200;
    }

    private async Task SubmitAddTrackAsync()
    {
        if (!int.TryParse(AddTrackDurationInput.Trim(), out var durationSec) || durationSec is < 1 or > 7200)
        {
            Status = "Длительность должна быть числом от 1 до 7200 секунд.";
            return;
        }

        int? albumId = null;
        int? trackNumber = null;
        var albumCoverInput = string.IsNullOrWhiteSpace(AddTrackAlbumCoverPath) ? null : AddTrackAlbumCoverPath.Trim();
        var isLocalAlbumCover = IsExistingLocalFile(albumCoverInput);

        if (IsAlbumTrackMode)
        {
            if (_draftAlbumId is null)
            {
                if (string.IsNullOrWhiteSpace(AddTrackAlbumTitle))
                {
                    Status = "Для альбома укажите название.";
                    return;
                }

                if (!int.TryParse(AddTrackPlannedCountInput.Trim(), out var plannedCount) || plannedCount < 1)
                {
                    Status = "Количество треков в альбоме должно быть больше 0.";
                    return;
                }

                var (album, albumError) = await _authSessionService.ApiClient.CreateMyAlbumAsync(new CreateAlbumRequestDto
                {
                    Title = AddTrackAlbumTitle.Trim(),
                    CoverPath = isLocalAlbumCover ? null : albumCoverInput
                });

                if (!string.IsNullOrWhiteSpace(albumError) || album is null)
                {
                    Status = $"Не удалось создать альбом: {albumError}";
                    return;
                }

                if (isLocalAlbumCover && albumCoverInput is not null)
                {
                    var uploadAlbumCoverError = await _authSessionService.ApiClient.UploadAlbumCoverAsync(album.Id, albumCoverInput);
                    if (!string.IsNullOrWhiteSpace(uploadAlbumCoverError))
                    {
                        Status = $"Альбом создан, но обложка не загружена: {uploadAlbumCoverError}";
                        return;
                    }
                }

                _draftAlbumId = album.Id;
                _albumTracksTotal = plannedCount;
                _albumTracksRemaining = plannedCount;
                OnPropertyChanged(nameof(AddTrackProgressText));
            }

            if (_albumTracksRemaining <= 0)
            {
                Status = "Укажите новое количество треков для следующего альбома.";
                _draftAlbumId = null;
                return;
            }

            albumId = _draftAlbumId;
            trackNumber = _albumTracksTotal - _albumTracksRemaining + 1;
        }

        var localPath = AddTrackLocalPath.Trim();
        var streamUrl = AddTrackStreamUrl.Trim();
        var sourceType = AddTrackIsOnlineSource ? "Online" : "Local";
        var selectedGenreIds = AddTrackGenres.Where(g => g.IsSelected).Select(g => g.Id).ToArray();
        var requestedCoverPath = IsAlbumTrackMode
            ? (string.IsNullOrWhiteSpace(AddTrackAlbumCoverPath) ? null : AddTrackAlbumCoverPath.Trim())
            : (string.IsNullOrWhiteSpace(AddTrackCoverPath) ? null : AddTrackCoverPath.Trim());
        var isLocalTrackFile = !AddTrackIsOnlineSource && IsExistingLocalFile(localPath);
        var isLocalTrackCover = IsExistingLocalFile(requestedCoverPath);
        var coverPath = isLocalTrackCover ? null : requestedCoverPath;
        var requestLocalPath = AddTrackIsOnlineSource
            ? null
            : (isLocalTrackFile ? null : (string.IsNullOrWhiteSpace(localPath) ? null : localPath));

        var (createdSong, createError) = await _authSessionService.ApiClient.CreateMySongAsync(new CreateSongRequestDto
        {
            AlbumId = albumId,
            Title = AddTrackTitleInput.Trim(),
            DurationSec = durationSec,
            SourceType = sourceType,
            LocalPath = requestLocalPath,
            StreamUrl = AddTrackIsOnlineSource ? (string.IsNullOrWhiteSpace(streamUrl) ? null : streamUrl) : null,
            CoverPath = coverPath,
            TrackNumber = trackNumber,
            Explicit = AddTrackExplicit,
            GenreIds = selectedGenreIds
        });

        if (!string.IsNullOrWhiteSpace(createError))
        {
            Status = $"Ошибка добавления трека: {createError}";
            return;
        }

        if (createdSong is null)
        {
            Status = "Трек создан с ошибкой ответа API.";
            return;
        }

        if (isLocalTrackFile)
        {
            var uploadAudioError = await _authSessionService.ApiClient.UploadSongAudioAsync(createdSong.Id, localPath);
            if (!string.IsNullOrWhiteSpace(uploadAudioError))
            {
                Status = $"Трек создан, но аудио не загружено: {uploadAudioError}";
                return;
            }
        }

        if (isLocalTrackCover && requestedCoverPath is not null)
        {
            var uploadCoverError = await _authSessionService.ApiClient.UploadSongCoverAsync(createdSong.Id, requestedCoverPath);
            if (!string.IsNullOrWhiteSpace(uploadCoverError))
            {
                Status = $"Трек создан, но обложка не загружена: {uploadCoverError}";
                return;
            }
        }

        await LoadTracksAsync();
        AddTrackTitleInput = string.Empty;

        if (IsAlbumTrackMode)
        {
            _albumTracksRemaining--;
            OnPropertyChanged(nameof(AddTrackProgressText));

            if (_albumTracksRemaining > 0)
            {
                Status = $"Трек добавлен. Осталось добавить: {_albumTracksRemaining}.";
                return;
            }

            Status = "Все треки для альбома добавлены. Можно начать новый альбом.";
            _draftAlbumId = null;
            _albumTracksTotal = 0;
            AddTrackAlbumTitle = string.Empty;
            AddTrackAlbumCoverPath = string.Empty;
            AddTrackPlannedCountInput = "1";
            OnPropertyChanged(nameof(AddTrackProgressText));
            return;
        }

        Status = "Трек добавлен. Окно оставлено открытым для следующего трека.";
    }

    private static bool IsExistingLocalFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return File.Exists(path.Trim());
        }
        catch
        {
            return false;
        }
    }

    private void RefreshFilteredAddTrackGenres()
    {
        var needle = AddTrackGenreSearchInput.Trim();
        var filtered = string.IsNullOrWhiteSpace(needle)
            ? AddTrackGenres
            : new ObservableCollection<AddTrackGenreItemViewModel>(AddTrackGenres.Where(g =>
                g.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)));

        FilteredAddTrackGenres.Clear();
        foreach (var genre in filtered)
            FilteredAddTrackGenres.Add(genre);
    }

    private void TryApplyDurationFromLocalAudio(string? filePath)
    {
        if (AddTrackIsOnlineSource || string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            var path = filePath.Trim();
            if (!File.Exists(path))
                return;

            using var file = TagLibFile.Create(path);
            var seconds = Math.Max(1, (int)Math.Round(file.Properties.Duration.TotalSeconds));
            AddTrackDurationInput = seconds.ToString();
        }
        catch
        {
        }
    }

    private bool CanChangePassword()
    {
        return !IsBusy
               && !string.IsNullOrWhiteSpace(CurrentPasswordInput)
               && !string.IsNullOrWhiteSpace(NewPasswordInput)
               && !string.IsNullOrWhiteSpace(ConfirmPasswordInput);
    }

    private async Task ChangePasswordAsync()
    {
        var error = await _authSessionService.ApiClient.ChangePasswordAsync(new ChangePasswordRequestDto
        {
            CurrentPassword = CurrentPasswordInput,
            NewPassword = NewPasswordInput,
            ConfirmNewPassword = ConfirmPasswordInput
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка смены пароля: {error}";
            return;
        }

        CurrentPasswordInput = string.Empty;
        NewPasswordInput = string.Empty;
        ConfirmPasswordInput = string.Empty;
        Status = "Пароль успешно изменен.";
    }

    private bool CanSaveArtistName()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(SettingsArtistNameInput);
    }

    private async Task SaveArtistNameAsync()
    {
        var artistName = SettingsArtistNameInput.Trim();
        if (string.IsNullOrWhiteSpace(artistName))
            return;

        if (IsArtistOrAdmin)
        {
            var (profile, updateError) = await _authSessionService.ApiClient.UpdateProfileMeAsync(new UpdateProfileRequestDto
            {
                DisplayName = DisplayName,
                ArtistName = artistName,
                Email = ProfileEmail,
                Phone = ProfilePhone
            });

            if (!string.IsNullOrWhiteSpace(updateError))
            {
                Status = $"Ошибка сохранения имени артиста: {updateError}";
                return;
            }

            ArtistName = profile?.ArtistName ?? artistName;
            Status = "Имя артиста обновлено.";
            return;
        }

        var roleError = await _authSessionService.ApiClient.SetArtistRoleAsync(new UsersPanelRoleRequestDto
        {
            ArtistName = artistName
        });

        if (!string.IsNullOrWhiteSpace(roleError))
        {
            Status = $"Ошибка перехода в артиста: {roleError}";
            return;
        }

        await LoadProfileAsync();
        Status = "Роль артиста активирована.";
    }

    private bool CanSaveProfileChanges()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(EditDisplayName);
    }

    private bool CanSaveContacts()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(EditEmailInput);
    }

    private async Task SaveContactsAsync()
    {
        var email = EditEmailInput.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var (profile, error) = await _authSessionService.ApiClient.UpdateProfileMeAsync(new UpdateProfileRequestDto
        {
            DisplayName = DisplayName,
            ArtistName = ArtistName,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(EditPhoneInput) ? null : EditPhoneInput.Trim()
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка сохранения контактов: {error}";
            return;
        }

        ProfileEmail = profile?.Email ?? email;
        ProfilePhone = profile?.Phone ?? (string.IsNullOrWhiteSpace(EditPhoneInput) ? null : EditPhoneInput.Trim());
        EditEmailInput = ProfileEmail;
        EditPhoneInput = ProfilePhone ?? string.Empty;
        IsEditContactsModalOpen = false;
        Status = "Контакты обновлены.";
    }

    private async Task SaveProfileChangesAsync()
    {
        var displayName = EditDisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return;

        var (profile, error) = await _authSessionService.ApiClient.UpdateProfileMeAsync(new UpdateProfileRequestDto
        {
            DisplayName = displayName,
            ArtistName = ArtistName,
            Email = ProfileEmail,
            Phone = ProfilePhone
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка сохранения профиля: {error}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(EditAvatarPath))
        {
            var avatarPath = EditAvatarPath.Trim();
            if (File.Exists(avatarPath))
            {
                UserAvatarSource = new Uri(avatarPath).AbsoluteUri;
                ApplyAvatarBitmapFromResolvedSource();
            }

            var avatarError = await _authSessionService.ApiClient.UploadProfileAvatarAsync(EditAvatarPath.Trim());
            if (!string.IsNullOrWhiteSpace(avatarError))
            {
                Status = $"Профиль сохранен, но аватар не загружен: {avatarError}";
                await LoadProfileAsync();
                return;
            }
        }

        DisplayName = profile?.DisplayName ?? displayName;
        EditAvatarPath = string.Empty;
        await LoadProfileAsync();
        IsEditProfileModalOpen = false;
        Status = "Профиль успешно обновлен.";
    }

    private async Task LogoutAsync()
    {
        await SaveSettingsAsync();
        _audioPlayer.Stop();
        var session = await _authSessionService.SessionStore.TryLoadAsync();
        if (session is not null && !string.IsNullOrWhiteSpace(session.RefreshToken))
            await _authSessionService.ApiClient.LogoutAsync(session.RefreshToken);

        await _authSessionService.SessionStore.ClearAsync();
        _authSessionService.ApiClient.SetAccessToken(null);
        await _onLogout();
    }

    private async Task SaveSettingsAsync() => await _playerSettingsStore.SaveAsync(new PlayerSettingsSnapshot
    {
        Volume = VolumePercent / 100d,
        IsMuted = IsMuted,
        AllowExplicitContent = AllowExplicitContent,
        LastTrackId = CurrentTrack?.Id ?? SelectedTrack?.Id ?? 0
    });

    private void UpdatePlayback()
    {
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentTrackArtist));
        OnPropertyChanged(nameof(CurrentTrackCoverImage));
        UpdateNowPlayingPreview();
        OnPropertyChanged(nameof(IsPlaybackActive));
        OnPropertyChanged(nameof(IsPlaybackInactive));
        RaiseCanExecutes();
    }

    private void UpdateTime()
    {
        _isSeeking = true;
        DurationSeconds = Math.Max(0, _audioPlayer.Duration.TotalSeconds);
        PositionSeconds = Math.Max(0, _audioPlayer.Position.TotalSeconds);
        _isSeeking = false;

        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(SeekPreviewText));
        if (!_isSeeking)
        {
            _seekPreviewSeconds = PositionSeconds;
            OnPropertyChanged(nameof(SeekPreviewText));
        }

        _ = ReportListeningProgressAsync();
    }

    private void RaiseCanExecutes()
    {
        RefreshTracksCommand.RaiseCanExecuteChanged();
        SearchTracksCommand.RaiseCanExecuteChanged();
        LogoutCommand.RaiseCanExecuteChanged();
        LikeSelectedTrackCommand.RaiseCanExecuteChanged();
        AddToQueueCommand.RaiseCanExecuteChanged();
        RemoveFromQueueCommand.RaiseCanExecuteChanged();
        MoveQueueUpCommand.RaiseCanExecuteChanged();
        MoveQueueDownCommand.RaiseCanExecuteChanged();
        ClearQueueCommand.RaiseCanExecuteChanged();
        CreatePlaylistCommand.RaiseCanExecuteChanged();
        SavePlaylistModalCommand.RaiseCanExecuteChanged();
        DeletePlaylistCommand.RaiseCanExecuteChanged();
        OpenEditPlaylistModalCommand.RaiseCanExecuteChanged();
        AddSelectedTrackToPlaylistCommand.RaiseCanExecuteChanged();
        RemovePlaylistTrackCommand.RaiseCanExecuteChanged();
        OpenSelectedArtistCommand.RaiseCanExecuteChanged();
        OpenAlbumArtistCommand.RaiseCanExecuteChanged();
        ToggleArtistFollowCommand.RaiseCanExecuteChanged();
        OpenSelectedAlbumCommand.RaiseCanExecuteChanged();
        OpenArtistAlbumCommand.RaiseCanExecuteChanged();
        PlaySelectedTrackCommand.RaiseCanExecuteChanged();
        PlayLikedTrackCommand.RaiseCanExecuteChanged();
        PlayQueueTrackCommand.RaiseCanExecuteChanged();
        PlayPlaylistTrackCommand.RaiseCanExecuteChanged();
        PlayArtistTrackCommand.RaiseCanExecuteChanged();
        PlayAlbumTrackCommand.RaiseCanExecuteChanged();
        PlayPauseCommand.RaiseCanExecuteChanged();
        NextTrackCommand.RaiseCanExecuteChanged();
        PreviousTrackCommand.RaiseCanExecuteChanged();
        AddCurrentTrackToLikedCommand.RaiseCanExecuteChanged();
        AddCurrentTrackToPlaylistCommand.RaiseCanExecuteChanged();
        SubmitAddTrackCommand.RaiseCanExecuteChanged();
        ChangePasswordCommand.RaiseCanExecuteChanged();
        SaveArtistNameCommand.RaiseCanExecuteChanged();
        SaveProfileChangesCommand.RaiseCanExecuteChanged();
        SaveContactsCommand.RaiseCanExecuteChanged();
        SelectFreePlanCommand.RaiseCanExecuteChanged();
        SelectStudentPlanCommand.RaiseCanExecuteChanged();
        SelectPremiumPlanCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(LikeButtonText));
        OnPropertyChanged(nameof(CanMoveQueueUp));
        OnPropertyChanged(nameof(CanMoveQueueDown));
        OnPropertyChanged(nameof(CurrentArtistMonthlyListenersText));
        OnPropertyChanged(nameof(AlbumTotalPlays));
        OnPropertyChanged(nameof(AlbumTotalPlaysText));
        OnPropertyChanged(nameof(ArtistMonthlyStreamsText));
        OnPropertyChanged(nameof(ArtistFollowersText));
        OnPropertyChanged(nameof(ArtistFollowButtonText));
        OnPropertyChanged(nameof(ArtistAvatarImage));
        OnPropertyChanged(nameof(PlaylistCoverImage));
        OnPropertyChanged(nameof(PlaylistTitleText));
        OnPropertyChanged(nameof(PlaylistMetaText));
        OnPropertyChanged(nameof(IsArtistReleaseAllFilter));
        OnPropertyChanged(nameof(IsArtistReleaseAlbumFilter));
        OnPropertyChanged(nameof(IsArtistReleaseSingleFilter));
        OnPropertyChanged(nameof(FilteredArtistReleases));
        OnPropertyChanged(nameof(VisibleArtistReleases));
        OnPropertyChanged(nameof(CanShowAllArtistReleases));
    }
}


