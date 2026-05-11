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
    private async Task InitializeAsync()
    {
        await LoadProfileAsync();

        var settings = await _playerSettingsStore.LoadAsync();
        VolumePercent = (int)Math.Round(Math.Clamp(settings.Volume, 0.0, 1.0) * 100);
        IsMuted = settings.IsMuted;
        AllowExplicitContent = settings.AllowExplicitContent;
        _restoredLastTrackId = settings.LastTrackId;

        ApplyFixedHomeCategories();
        await LoadTracksAsync();
        await LoadLikedAsync();
        await LoadQueueAsync();
        await LoadPlaylistsAsync();
        await LoadSubscriptionPlansAsync();
        await LoadAddTrackGenresAsync();
        await LoadFollowingArtistsAsync();
        await LoadCurrentSubscriptionAsync();
        await LoadHomeLibraryHighlightsAsync();
        _isInitializing = false;
    }


    private async Task LoadProfileAsync()
    {
        var (profile, error) = await _authSessionService.ApiClient.GetProfileMeAsync();
        if (!string.IsNullOrWhiteSpace(error) || profile is null)
            return;

        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? DisplayName : profile.DisplayName;
        RoleTitle = string.IsNullOrWhiteSpace(profile.RoleTitle) ? RoleTitle : profile.RoleTitle;
        ArtistName = profile.ArtistName;
        ProfileLogin = profile.Login;
        ProfileEmail = profile.Email;
        ProfilePhone = profile.Phone;
        IsContactsVisible = false;
        SettingsArtistNameInput = profile.ArtistName ?? string.Empty;
        EditDisplayName = DisplayName;
        EditAvatarPath = string.Empty;
        EditEmailInput = ProfileEmail;
        EditPhoneInput = ProfilePhone ?? string.Empty;
        UserAvatarSource = ResolveAvatarDisplaySource(profile.AvatarPath);
        ApplyAvatarBitmapFromResolvedSource();
    }

    private async Task LoadFollowingArtistsAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetFollowingArtistsAsync();
        if (!string.IsNullOrWhiteSpace(error))
            return;

        FollowingArtists.Clear();
        foreach (var item in items.OrderBy(x => x.ArtistName, StringComparer.OrdinalIgnoreCase))
        {
            item.AvatarPath = ResolveAvatarDisplaySource(item.AvatarPath);
            try
            {
                var localAvatar = TryResolveAvatarByFileName(item.AvatarPath ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(localAvatar) && File.Exists(localAvatar))
                    item.AvatarBitmap = new Bitmap(localAvatar);
            }
            catch
            {
                item.AvatarBitmap = null;
            }

            FollowingArtists.Add(item);
        }

        FollowingArtistsCount = items.Count;
    }

    private async Task LoadCurrentSubscriptionAsync()
    {
        var (subscription, error) = await _authSessionService.ApiClient.GetMySubscriptionAsync();
        if (!string.IsNullOrWhiteSpace(error) || subscription is null)
            return;

        CurrentSubscription = subscription;
    }
    private void ApplyFixedHomeCategories()
    {
        GenreOptions.Clear();
        GenreOptions.Add("Все");
        SelectedGenre = "Все";
    }

    private async Task LoadTracksAsync()
    {
        IsBusy = true;
        try
        {
            var genreFilter = string.Equals(SelectedGenre, "Все", StringComparison.OrdinalIgnoreCase) ? null : SelectedGenre;
            var (items, error) = await _authSessionService.ApiClient.GetTracksAsync(SearchText, genreFilter, "title");
            await HydrateAlbumTitlesAsync(items);
            Tracks.Clear();
            foreach (var item in items.Where(CanShowTrackForCurrentSettings)) Tracks.Add(item);

            Status = string.IsNullOrWhiteSpace(error)
                ? "Треки обновлены."
                : $"Ошибка треков: {error}";

            await BuildSearchResultsAsync();
            RefreshOverviewGenresFromTracks();
            await RefreshOverviewShelvesAsync();
            await BuildPersonalRecommendationsAsync();
            if (SelectedTrack is null && _restoredLastTrackId > 0)
                SelectedTrack = Tracks.FirstOrDefault(t => t.Id == _restoredLastTrackId);
            if (Tracks.Count > 0 && SelectedTrack is null)
                SelectedTrack = Tracks[0];
            SeedRecentTracks();
            UpdateNowPlayingPreview();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanShowTrackForCurrentSettings(TrackListItemDto track)
    {
        if (!AllowExplicitContent && track.Explicit)
            return false;
        return true;
    }

    private void RefreshOverviewGenresFromTracks()
    {
        var genres = Tracks
            .SelectMany(t => t.GenreTitles ?? Array.Empty<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GenreOptions.Clear();
        GenreOptions.Add("Все");
        foreach (var genre in genres)
            GenreOptions.Add(genre);
    }

    private async Task SearchTracksAsync()
    {
        await LoadTracksAsync();
        ActiveSection = string.IsNullOrWhiteSpace(SearchText) ? "tracks" : "search";
    }

    private async Task RefreshOverviewShelvesAsync()
    {
        var playlistTrackIds = new HashSet<int>();
        foreach (var playlist in Playlists)
        {
            var (tracks, error) = await _authSessionService.ApiClient.GetPlaylistTracksAsync(playlist.Id);
            if (!string.IsNullOrWhiteSpace(error))
                continue;

            foreach (var track in tracks)
                playlistTrackIds.Add(track.Id);
        }

        var genreScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in LikedTracks)
        {
            foreach (var genre in track.GenreTitles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(genre))
                    continue;
                genreScores[genre] = genreScores.TryGetValue(genre, out var score) ? score + 4 : 4;
            }
        }

        foreach (var track in RecentTracks)
        {
            foreach (var genre in track.GenreTitles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(genre))
                    continue;
                genreScores[genre] = genreScores.TryGetValue(genre, out var score) ? score + 2 : 2;
            }
        }

        var genreToTracks = Tracks
            .Where(track => !_likedSongIds.Contains(track.Id))
            .Where(track => !playlistTrackIds.Contains(track.Id))
            .SelectMany(track => (track.GenreTitles ?? Array.Empty<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(genre => new { Genre = genre, Track = track }))
            .GroupBy(x => x.Genre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Track).DistinctBy(t => t.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var orderedGenres = genreToTracks.Keys
            .OrderByDescending(g => genreScores.TryGetValue(g, out var score) ? score : 0)
            .ThenByDescending(g => genreToTracks[g].Count)
            .ThenBy(g => g, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        OverviewGenreShelves.Clear();
        foreach (var genre in orderedGenres)
        {
            var tracks = genreToTracks[genre]
                .OrderByDescending(t => _likedSongIds.Contains(t.Id))
                .ThenByDescending(t => Math.Max(0, t.PlayCount))
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            if (tracks.Count == 0)
                continue;

            OverviewGenreShelves.Add(new OverviewGenreShelfDto
            {
                Genre = genre,
                Tracks = tracks
            });
        }
    }

    public async Task OpenOverviewGenreAsync(string genre)
    {
        SelectedGenre = string.IsNullOrWhiteSpace(genre) ? "Все" : genre;
        IsOverviewOpen = false;
        ActiveSection = "tracks";

        var (items, error) = await _authSessionService.ApiClient.GetTracksAsync(null,
            string.Equals(SelectedGenre, "Все", StringComparison.OrdinalIgnoreCase) ? null : SelectedGenre, "plays");
        await HydrateAlbumTitlesAsync(items);
        Tracks.Clear();
        foreach (var item in items.Where(CanShowTrackForCurrentSettings))
            Tracks.Add(item);
        Status = string.IsNullOrWhiteSpace(error) ? $"Жанр: {SelectedGenre}" : $"Ошибка жанра: {error}";
    }

    private async Task LoadSubscriptionPlansAsync()
    {
        var (plans, error) = await _authSessionService.ApiClient.GetSubscriptionPlansAsync();
        if (!string.IsNullOrWhiteSpace(error))
        {
            Status = $"Ошибка подписок: {error}";
            return;
        }

        SubscriptionPlans.Clear();
        foreach (var plan in plans)
            SubscriptionPlans.Add(plan);

        FreePlan = SubscriptionPlans.FirstOrDefault(p => p.IsFree)
                   ?? SubscriptionPlans.FirstOrDefault(p => ContainsToken(p.Title, "free", "бесплат", "индивидуальн"));
        StudentPlan = SubscriptionPlans.FirstOrDefault(p => ContainsToken(p.Title, "student", "студент"));
        PremiumPlan = SubscriptionPlans.FirstOrDefault(p => !p.IsFree && p != StudentPlan)
                      ?? SubscriptionPlans.FirstOrDefault(p => ContainsToken(p.Title, "premium", "премиум"));
        SelectedSubscriptionPlan = SubscriptionPlans.FirstOrDefault(p => p.IsFree) ?? SubscriptionPlans.FirstOrDefault();
    }

    private async Task LoadAddTrackGenresAsync()
    {
        var (genres, error) = await _authSessionService.ApiClient.GetGenresAsync();
        if (!string.IsNullOrWhiteSpace(error))
            return;

        AddTrackGenres.Clear();
        foreach (var genre in genres.OrderBy(g => g.Title))
        {
            AddTrackGenres.Add(new AddTrackGenreItemViewModel
            {
                Id = genre.Id,
                Title = genre.Title
            });
        }

        RefreshFilteredAddTrackGenres();
    }

    private async Task BuildSearchResultsAsync()
    {
        SearchResultTracks.Clear();
        SearchResultArtists.Clear();
        SearchResultAlbums.Clear();
        SearchResultPlaylists.Clear();

        foreach (var track in Tracks)
            SearchResultTracks.Add(track);

        var localArtists = Tracks
                     .Where(t => t.ArtistUserId > 0 && !string.IsNullOrWhiteSpace(t.Artist))
                     .GroupBy(t => t.ArtistUserId)
                     .OrderBy(g => g.First().Artist, StringComparer.OrdinalIgnoreCase)
                     .Select(artistGroup =>
                     {
                         var first = artistGroup.First();
                         return new ArtistSearchItemDto
                         {
                             ArtistUserId = artistGroup.Key,
                             ArtistName = first.Artist,
                             AvatarPath = artistGroup.Select(t => t.CoverPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)),
                             AvatarBitmap = artistGroup.Select(t => t.CoverBitmap).FirstOrDefault(bitmap => bitmap is not null),
                             TracksCount = artistGroup.Count()
                         };
                     })
                     .ToList();

        var needle = SearchText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(needle))
        {
            var (artistsFromApi, artistsError) = await _authSessionService.ApiClient.SearchArtistsAsync(needle);
            if (string.IsNullOrWhiteSpace(artistsError) && artistsFromApi.Count > 0)
            {
                localArtists = artistsFromApi.ToList();
            }
        }

        foreach (var artist in localArtists)
            SearchResultArtists.Add(artist);

        foreach (var albumGroup in Tracks
                     .Where(t => t.AlbumId.HasValue)
                     .GroupBy(t => t.AlbumId!.Value)
                     .OrderBy(g => g.Key))
        {
            var first = albumGroup.First();
            var albumCover = albumGroup.Select(t => t.CoverPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            SearchResultAlbums.Add(new AlbumListItemDto
            {
                Id = albumGroup.Key,
                Title = !string.IsNullOrWhiteSpace(first.AlbumTitle) ? first.AlbumTitle! : $"Альбом #{albumGroup.Key}",
                CoverPath = albumCover,
                CoverBitmap = albumGroup.Select(t => t.CoverBitmap).FirstOrDefault(b => b is not null),
                PlayCount = albumGroup.Sum(t => Math.Max(0, t.PlayCount))
            });
        }

        var ownPublicPlaylists = Playlists
            .Where(p => p.IsPublic)
            .Where(p => string.IsNullOrWhiteSpace(needle) ||
                        p.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.Description) &&
                         p.Description.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            .Select(p =>
            {
                p.OwnerUserId = _currentUserId;
                p.OwnerName = DisplayName;
                p.IsReadOnlyView = false;
                return p;
            })
            .ToList();

        var (publicFromApi, playlistsError) = await _authSessionService.ApiClient.GetPublicPlaylistsAsync(needle, 80);
        if (string.IsNullOrWhiteSpace(playlistsError))
        {
            var merged = ownPublicPlaylists
                .Concat(publicFromApi.Where(p => p.OwnerUserId != _currentUserId))
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var playlist in merged)
            {
                playlist.IsReadOnlyView = playlist.OwnerUserId != _currentUserId;
                SearchResultPlaylists.Add(playlist);
            }
        }
        else
        {
            foreach (var playlist in ownPublicPlaylists)
                SearchResultPlaylists.Add(playlist);
        }

        NotifySearchType();
    }
}




