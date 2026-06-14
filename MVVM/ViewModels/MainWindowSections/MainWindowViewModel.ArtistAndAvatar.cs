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
    // Переключает раздел или состояние интерфейса.
    private async Task OpenArtistByIdAsync(int artistUserId)
    {
        if (artistUserId <= 0)
        {
            Status = "Некорректный идентификатор артиста.";
            return;
        }

        var (artist, error) = await _authSessionService.ApiClient.GetArtistAsync(artistUserId);
        if (!string.IsNullOrWhiteSpace(error) || artist is null)
        {
            Status = $"Ошибка артиста: {error}";
            return;
        }

        _currentArtistUserId = artistUserId;
        ArtistHeader = artist.ArtistName;
        ArtistAvatarPath = artist.AvatarPath ?? string.Empty;
        ArtistAvatarBitmap = artist.AvatarBitmap;
        _artistFollowersCount = artist.FollowersCount;
        _isFollowingArtist = artist.IsFollowing;
        _artistReleaseFilter = "all";
        IsArtistReleasesModalOpen = false;
        ArtistTopTracks.Clear();
        ArtistAlbums.Clear();
        ArtistReleases.Clear();
        var trackOrder = 1;
        var totalStreams = 0;
        foreach (var t in artist.TopTracks)
        {
            t.TrackOrder = trackOrder++;
            totalStreams += Math.Max(0, t.PlayCount);
            ArtistTopTracks.Add(t);
        }
        _artistMonthlyStreams = totalStreams;
        _currentArtistPlaysTotal = totalStreams;
        foreach (var a in artist.Albums)
        {
            ArtistAlbums.Add(a);
            var albumPlays = artist.TopTracks
                .Where(t => t.AlbumId == a.Id)
                .Sum(t => Math.Max(0, t.PlayCount));
            ArtistReleases.Add(new ArtistReleaseItemDto
            {
                IsAlbum = true,
                AlbumId = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath,
                CoverBitmap = a.CoverBitmap,
                PlaysCount = albumPlays,
                ReleaseDate = a.ReleaseDate
            });
        }

        foreach (var single in artist.TopTracks.Where(t => t.AlbumId is null))
        {
            ArtistReleases.Add(new ArtistReleaseItemDto
            {
                IsAlbum = false,
                TrackId = single.Id,
                Title = single.Title,
                CoverPath = single.CoverPath,
                CoverBitmap = single.CoverBitmap,
                PlaysCount = Math.Max(0, single.PlayCount),
                ReleaseDate = null
            });
        }

        var sortedReleases = ArtistReleases
            .OrderByDescending(r => r.PlaysCount)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ArtistReleases.Clear();
        foreach (var release in sortedReleases)
            ArtistReleases.Add(release);

        SelectedArtistTrack = ArtistTopTracks.FirstOrDefault();
        SelectedArtistAlbum = ArtistAlbums.FirstOrDefault();
        if (SelectedArtistTrack is not null)
            SelectedTrack = SelectedArtistTrack;
        ArtistHeroCoverPath = ArtistAvatarPath;
        if (string.IsNullOrWhiteSpace(ArtistHeroCoverPath))
        {
            ArtistHeroCoverPath = artist.Albums.FirstOrDefault()?.CoverSource
                ?? artist.TopTracks.FirstOrDefault()?.CoverSource
                ?? string.Empty;
        }
        ActiveSection = "artist";
        OnPropertyChanged(nameof(CurrentArtistTotalStreamsText));
        OnPropertyChanged(nameof(ArtistMonthlyStreamsText));
        RaiseCanExecutes();
    }

    // Выполняет внутреннюю логику метода.
    private void NotifySections()
    {
        OnPropertyChanged(nameof(IsTracksSection));
        OnPropertyChanged(nameof(IsSearchSection));
        OnPropertyChanged(nameof(IsPremiumSection));
        OnPropertyChanged(nameof(IsLikedSection));
        OnPropertyChanged(nameof(IsQueueSection));
        OnPropertyChanged(nameof(IsPlaylistsSection));
        OnPropertyChanged(nameof(IsArtistSection));
        OnPropertyChanged(nameof(IsAlbumSection));
        OnPropertyChanged(nameof(IsProfileSection));
        OnPropertyChanged(nameof(IsSettingsSection));
    }

    // Выполняет внутреннюю логику метода.
    private void NotifySearchType()
    {
        OnPropertyChanged(nameof(IsSearchAllType));
        OnPropertyChanged(nameof(IsSearchArtistsType));
        OnPropertyChanged(nameof(IsSearchTracksType));
        OnPropertyChanged(nameof(IsSearchAlbumsType));
        OnPropertyChanged(nameof(IsSearchPlaylistsType));
    }

    // Выполняет внутреннюю логику метода.
    private void SeedRecentTracks()
    {
        if (RecentTracks.Count > 0)
            return;

        foreach (var track in Tracks.Take(8))
            RecentTracks.Add(track);
    }

    // Выполняет внутреннюю логику метода.
    private void RememberTrack(TrackListItemDto track)
    {
        var existing = RecentTracks.FirstOrDefault(x => x.Id == track.Id);
        if (existing is not null)
            RecentTracks.Remove(existing);
        RecentTracks.Insert(0, track);
        while (RecentTracks.Count > 8)
            RecentTracks.RemoveAt(RecentTracks.Count - 1);
    }

    // Переключает раздел или состояние интерфейса.
    private async Task ToggleArtistFollowAsync()
    {
        if (_currentArtistUserId <= 0)
            return;

        var (state, error) = _isFollowingArtist
            ? await _authSessionService.ApiClient.UnfollowArtistAsync(_currentArtistUserId)
            : await _authSessionService.ApiClient.FollowArtistAsync(_currentArtistUserId);

        if (!string.IsNullOrWhiteSpace(error) || state is null)
        {
            Status = $"Ошибка подписки: {error}";
            return;
        }

        _isFollowingArtist = state.IsFollowing;
        _artistFollowersCount = state.FollowersCount;
        await LoadFollowingArtistsAsync();
        Status = _isFollowingArtist ? "Подписка оформлена." : "Подписка отменена.";
        RaiseCanExecutes();
    }

    // Обновляет состояние и приводит данные к нужному виду.
    private void SetArtistReleaseFilter(string filter)
    {
        _artistReleaseFilter = filter;

        OnPropertyChanged(nameof(IsArtistReleaseAllFilter));
        OnPropertyChanged(nameof(IsArtistReleaseAlbumFilter));
        OnPropertyChanged(nameof(IsArtistReleaseSingleFilter));
        OnPropertyChanged(nameof(FilteredArtistReleases));
        OnPropertyChanged(nameof(VisibleArtistReleases));
        OnPropertyChanged(nameof(CanShowAllArtistReleases));
    }

    // Переключает раздел или состояние интерфейса.
    private void OpenArtistReleasesModal()
    {
        if (FilteredArtistReleases.Count == 0)
            return;
        IsArtistReleasesModalOpen = true;
    }

    // Выполняет внутреннюю логику метода.
    private static bool ContainsToken(string? source, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        foreach (var token in tokens)
        {
            if (source.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // Обновляет состояние и приводит данные к нужному виду.
    private string? ResolveAvatarDisplaySource(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
            return null;

        try
        {
            var localByFileName = TryResolveAvatarByFileName(avatarPath);
            if (!string.IsNullOrWhiteSpace(localByFileName) && File.Exists(localByFileName))
                return new Uri(localByFileName).AbsoluteUri;

            if (Uri.TryCreate(avatarPath, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.IsFile && File.Exists(absoluteUri.LocalPath))
                    return absoluteUri.AbsoluteUri;

                var url = absoluteUri.ToString();
                var cacheStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return url.Contains('?') ? $"{url}&v={cacheStamp}" : $"{url}?v={cacheStamp}";
            }

            var relative = avatarPath.TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var localUpload = TryResolveUploadLocalPath(relative);
            if (!string.IsNullOrWhiteSpace(localUpload) && File.Exists(localUpload))
                return new Uri(localUpload).AbsoluteUri;

            var apiUrl = _authSessionService.ApiClient.ResolveAssetUrl(avatarPath);
            if (string.IsNullOrWhiteSpace(apiUrl))
                return null;
            var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return apiUrl.Contains('?') ? $"{apiUrl}&v={stamp}" : $"{apiUrl}?v={stamp}";
        }
        catch
        {
            return null;
        }
    }

    // Обновляет состояние и приводит данные к нужному виду.
    public void SetAvatarPreviewFromLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        UserAvatarSource = new Uri(path).AbsoluteUri;
        ApplyAvatarBitmapFromResolvedSource();
    }

    // Выполняет внутреннюю логику метода.
    private void ApplyAvatarBitmapFromResolvedSource()
    {
        try
        {
            var source = UserAvatarSource;
            if (string.IsNullOrWhiteSpace(source))
            {
                UserAvatarBitmap = null;
                return;
            }

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                {
                    UserAvatarBitmap = new Bitmap(uri.LocalPath);
                    return;
                }

                var localByFileName = TryResolveAvatarByFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(localByFileName) && File.Exists(localByFileName))
                {
                    UserAvatarBitmap = new Bitmap(localByFileName);
                    return;
                }
            }

            var local = TryResolveAvatarByFileName(source);
            if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
            {
                UserAvatarBitmap = new Bitmap(local);
                return;
            }

            UserAvatarBitmap = null;
        }
        catch
        {
            UserAvatarBitmap = null;
        }
    }

    // Выполняет внутреннюю логику метода.
    private static string? TryResolveUploadLocalPath(string relativePath)
    {
        var startPoints = new List<string>
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var start in startPoints.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "API", "wwwroot", relativePath);
                if (File.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }

        return null;
    }

    // Выполняет внутреннюю логику метода.
    private static string? TryResolveAvatarByFileName(string avatarPath)
    {
        try
        {
            var filename = Path.GetFileName(avatarPath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var startPoints = new List<string>
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            foreach (var start in startPoints.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = new DirectoryInfo(start);
                while (dir is not null)
                {
                    var avatarsDir = Path.Combine(dir.FullName, "API", "wwwroot", "uploads", "avatars");
                    var candidate = Path.Combine(avatarsDir, filename);
                    if (File.Exists(candidate))
                        return candidate;

                    dir = dir.Parent;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }


    // Обновляет состояние и приводит данные к нужному виду.
    public void UpdateSeekPreview(double previewSeconds)
    {
        _seekPreviewSeconds = Math.Clamp(previewSeconds, 0, DurationSeconds);
        if (!_isSeekPreviewVisible)
        {
            _isSeekPreviewVisible = true;
            OnPropertyChanged(nameof(IsSeekPreviewVisible));
        }

        OnPropertyChanged(nameof(SeekPreviewText));
    }

    // Выполняет внутреннюю логику метода.
    public void HideSeekPreview()
    {
        if (!_isSeekPreviewVisible) return;
        _isSeekPreviewVisible = false;
        OnPropertyChanged(nameof(IsSeekPreviewVisible));
    }
}




