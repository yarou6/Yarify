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
        IsEmailVisible = false;
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
        GenreOptions.Add("Музыка");
        GenreOptions.Add("Подкасты");
        GenreOptions.Add("Аудиокниги");
        SelectedGenre = "Все";
    }

    private async Task LoadTracksAsync()
    {
        IsBusy = true;
        try
        {
            var (items, error) = await _authSessionService.ApiClient.GetTracksAsync(SearchText, null, "title");
            await HydrateAlbumTitlesAsync(items);
            Tracks.Clear();
            foreach (var item in items) Tracks.Add(item);

            Status = string.IsNullOrWhiteSpace(error)
                ? "Треки обновлены."
                : $"Ошибка треков: {error}";

            await BuildSearchResultsAsync();
            if (Tracks.Count > 0 && SelectedTrack is null) SelectedTrack = Tracks[0];
            SeedRecentTracks();
            UpdateNowPlayingPreview();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchTracksAsync()
    {
        await LoadTracksAsync();
        ActiveSection = string.IsNullOrWhiteSpace(SearchText) ? "tracks" : "search";
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
                CoverBitmap = albumGroup.Select(t => t.CoverBitmap).FirstOrDefault(b => b is not null)
            });
        }

        var playlists = string.IsNullOrWhiteSpace(needle)
            ? Playlists
            : new ObservableCollection<PlaylistListItemDto>(Playlists.Where(p =>
                p.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(p.Description) &&
                 p.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))));

        foreach (var playlist in playlists)
            SearchResultPlaylists.Add(playlist);

        NotifySearchType();
    }
}




