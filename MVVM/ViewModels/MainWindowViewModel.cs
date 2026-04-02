
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using MVVM.Models.Auth;
using MVVM.Models.Playback;
using MVVM.Models.Profile;
using MVVM.Models.Subscriptions;
using MVVM.Services;
using MVVM.Tools;

namespace MVVM.ViewModels;

public class MainWindowViewModel : BaseVM
{
    private readonly AuthSessionService _authSessionService;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly PlayerSettingsStore _playerSettingsStore;
    private readonly Func<Task> _onLogout;
    private readonly Random _random = new();
    private readonly HashSet<int> _likedSongIds = new();

    private TrackListItemDto? _selectedTrack;
    private TrackListItemDto? _selectedLikedTrack;
    private QueueItemDto? _selectedQueueItem;
    private PlaylistListItemDto? _selectedPlaylist;
    private TrackListItemDto? _selectedPlaylistTrack;
    private TrackListItemDto? _selectedArtistTrack;
    private TrackListItemDto? _selectedAlbumTrack;
    private AlbumListItemDto? _selectedArtistAlbum;
    private TrackListItemDto? _currentTrack;

    private string _displayName = "Yarify User";
    private string? _userAvatarSource;
    private string _roleTitle = "User";
    private string _status = "Вы в приложении.";
    private bool _isBusy;
    private bool _isInitializing = true;

    private int _volumePercent = 70;
    private bool _isMuted;
    private double _positionSeconds;
    private double _durationSeconds;
    private bool _isSeeking;

    private string _searchText = string.Empty;
    private string _selectedGenre = "Все";
    private string _selectedSearchType = "Все";
    private string _newPlaylistTitle = string.Empty;
    private string _newPlaylistDescription = string.Empty;
    private bool _newPlaylistIsPublic;
    private bool _isPlaylistModalOpen;
    private bool _isPlaylistEditMode;

    private bool _isSeekPreviewVisible;
    private double _seekPreviewSeconds;

    private string _artistHeroCoverPath = string.Empty;
    private string _albumCoverPath = string.Empty;

    private bool _isShuffleEnabled;
    private bool _isQueuePanelOpen = true;
    private PlaybackMode _playbackMode = PlaybackMode.Normal;
    private string _activeSection = "tracks";
    private string _artistHeader = "Артист";
    private string _albumHeader = "Альбом";
    private bool _isOverviewOpen;
    private SubscriptionPlanDto? _selectedSubscriptionPlan;
    private SubscriptionPlanDto? _freePlan;
    private SubscriptionPlanDto? _studentPlan;
    private SubscriptionPlanDto? _premiumPlan;

    public MainWindowViewModel(AuthSessionService authSessionService, AuthResponseDto authData, IAudioPlayerService audioPlayer, PlayerSettingsStore playerSettingsStore, Func<Task> onLogout)
    {
        _authSessionService = authSessionService;
        _audioPlayer = audioPlayer;
        _playerSettingsStore = playerSettingsStore;
        _onLogout = onLogout;

        Tracks = new ObservableCollection<TrackListItemDto>();
        LikedTracks = new ObservableCollection<TrackListItemDto>();
        QueueItems = new ObservableCollection<QueueItemDto>();
        Playlists = new ObservableCollection<PlaylistListItemDto>();
        PlaylistTracks = new ObservableCollection<TrackListItemDto>();
        ArtistTopTracks = new ObservableCollection<TrackListItemDto>();
        ArtistAlbums = new ObservableCollection<AlbumListItemDto>();
        AlbumTracks = new ObservableCollection<TrackListItemDto>();
        RecentTracks = new ObservableCollection<TrackListItemDto>();
        SubscriptionPlans = new ObservableCollection<SubscriptionPlanDto>();
        SearchResultTracks = new ObservableCollection<TrackListItemDto>();
        SearchResultArtists = new ObservableCollection<string>();
        SearchResultAlbums = new ObservableCollection<string>();
        SearchResultPlaylists = new ObservableCollection<PlaylistListItemDto>();

        GenreOptions = new ObservableCollection<string> { "Все", "Музыка", "Подкасты", "Аудиокниги" };
        SearchTypeOptions = new ObservableCollection<string> { "Все", "Исполнители", "Треки", "Альбомы", "Плейлисты" };

        DisplayName = $"Пользователь #{authData.UserId}";
        RoleTitle = authData.RoleTitle;

        RefreshTracksCommand = new AsyncRelayCommand(LoadTracksAsync, () => !IsBusy);
        SearchTracksCommand = new AsyncRelayCommand(SearchTracksAsync, () => !IsBusy);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsBusy);

        LikeSelectedTrackCommand = new AsyncRelayCommand(LikeSelectedTrackAsync, () => SelectedTrack is not null);
        AddToQueueCommand = new AsyncRelayCommand(AddSelectedToQueueAsync, () => SelectedTrack is not null);
        RemoveFromQueueCommand = new AsyncRelayCommand(RemoveSelectedQueueAsync, () => SelectedQueueItem is not null);
        MoveQueueUpCommand = new AsyncRelayCommand(MoveSelectedQueueUpAsync, () => CanMoveQueueUp);
        MoveQueueDownCommand = new AsyncRelayCommand(MoveSelectedQueueDownAsync, () => CanMoveQueueDown);
        ClearQueueCommand = new AsyncRelayCommand(ClearQueueAsync, () => QueueItems.Count > 0);

        CreatePlaylistCommand = new AsyncRelayCommand(CreatePlaylistAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        SavePlaylistModalCommand = new AsyncRelayCommand(SavePlaylistModalAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        OpenCreatePlaylistModalCommand = new RelayCommand(OpenCreatePlaylistModal);
        OpenEditPlaylistModalCommand = new RelayCommand(OpenEditPlaylistModal, () => SelectedPlaylist is not null);
        ClosePlaylistModalCommand = new RelayCommand(() => IsPlaylistModalOpen = false);
        DeletePlaylistCommand = new AsyncRelayCommand(DeleteSelectedPlaylistAsync, () => SelectedPlaylist is not null);
        AddSelectedTrackToPlaylistCommand = new AsyncRelayCommand(AddSelectedTrackToPlaylistAsync, () => SelectedTrack is not null && SelectedPlaylist is not null);
        RemovePlaylistTrackCommand = new AsyncRelayCommand(RemoveSelectedPlaylistTrackAsync, () => SelectedPlaylistTrack is not null && SelectedPlaylist is not null);

        OpenSelectedArtistCommand = new AsyncRelayCommand(OpenSelectedArtistAsync, () => (SelectedTrack ?? CurrentTrack) is not null);
        OpenSelectedAlbumCommand = new AsyncRelayCommand(OpenSelectedAlbumAsync, () => (SelectedTrack?.AlbumId ?? CurrentTrack?.AlbumId) is not null);
        OpenArtistAlbumCommand = new AsyncRelayCommand(OpenSelectedArtistAlbumAsync, () => SelectedArtistAlbum is not null);

        PlaySelectedTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedTrack), () => SelectedTrack is not null);
        PlayLikedTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedLikedTrack), () => SelectedLikedTrack is not null);
        PlayQueueTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedQueueItem?.Track), () => SelectedQueueItem is not null);
        PlayPlaylistTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedPlaylistTrack), () => SelectedPlaylistTrack is not null);
        PlayArtistTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedArtistTrack), () => SelectedArtistTrack is not null);
        PlayAlbumTrackCommand = new AsyncRelayCommand(async () => await PlayTrackAsync(SelectedAlbumTrack), () => SelectedAlbumTrack is not null);

        SetSectionTracksCommand = new RelayCommand(() =>
        {
            ActiveSection = "tracks";
            IsOverviewOpen = false;
        });
        SetSectionBrowseCommand = new RelayCommand(() =>
        {
            ActiveSection = "tracks";
            IsOverviewOpen = !IsOverviewOpen;
        });
        SetSectionPremiumCommand = new RelayCommand(() => ActiveSection = "premium");
        SetSectionLikedCommand = new RelayCommand(() => ActiveSection = "liked");
        SetSectionQueueCommand = new RelayCommand(() => ActiveSection = "queue");
        ToggleQueuePanelCommand = new RelayCommand(() => IsQueuePanelOpen = !IsQueuePanelOpen);
        SetSectionPlaylistsCommand = new RelayCommand(() => ActiveSection = "playlists");
        SetSectionArtistCommand = new RelayCommand(() => ActiveSection = "artist");
        SetSectionAlbumCommand = new RelayCommand(() => ActiveSection = "album");
        AddCurrentTrackToLikedCommand = new AsyncRelayCommand(AddCurrentTrackToLikedAsync, () => CurrentTrack is not null);
        AddCurrentTrackToPlaylistCommand = new AsyncRelayCommand(AddCurrentTrackToPlaylistAsync, () => CurrentTrack is not null && SelectedPlaylist is not null);

        PlayPauseCommand = new RelayCommand(PlayPause, () => CurrentTrack is not null);
        NextTrackCommand = new RelayCommand(() => _ = PlayNextTrackAsync());
        PreviousTrackCommand = new RelayCommand(PlayPreviousTrack);
        MuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        ToggleShuffleCommand = new RelayCommand(() => IsShuffleEnabled = !IsShuffleEnabled);
        ToggleRepeatModeCommand = new RelayCommand(ToggleRepeatMode);

        _audioPlayer.PlaybackStateChanged += (_, _) => Dispatcher.UIThread.Post(UpdatePlayback);
        _audioPlayer.PositionChanged += (_, _) => Dispatcher.UIThread.Post(UpdateTime);
        _audioPlayer.TrackEnded += (_, _) => Dispatcher.UIThread.Post(async () => await PlayNextTrackAsync());

        _ = InitializeAsync();
    }

    public ObservableCollection<TrackListItemDto> Tracks { get; }
    public ObservableCollection<TrackListItemDto> LikedTracks { get; }
    public ObservableCollection<QueueItemDto> QueueItems { get; }
    public ObservableCollection<PlaylistListItemDto> Playlists { get; }
    public ObservableCollection<TrackListItemDto> PlaylistTracks { get; }
    public ObservableCollection<TrackListItemDto> ArtistTopTracks { get; }
    public ObservableCollection<AlbumListItemDto> ArtistAlbums { get; }
    public ObservableCollection<TrackListItemDto> AlbumTracks { get; }
    public ObservableCollection<TrackListItemDto> RecentTracks { get; }
    public ObservableCollection<string> GenreOptions { get; }
    public ObservableCollection<SubscriptionPlanDto> SubscriptionPlans { get; }
    public ObservableCollection<TrackListItemDto> SearchResultTracks { get; }
    public ObservableCollection<string> SearchResultArtists { get; }
    public ObservableCollection<string> SearchResultAlbums { get; }
    public ObservableCollection<PlaylistListItemDto> SearchResultPlaylists { get; }
    public ObservableCollection<string> SearchTypeOptions { get; }
    public TrackListItemDto? SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value, RaiseCanExecutes); }
    public TrackListItemDto? SelectedLikedTrack { get => _selectedLikedTrack; set => SetProperty(ref _selectedLikedTrack, value, RaiseCanExecutes); }
    public QueueItemDto? SelectedQueueItem { get => _selectedQueueItem; set => SetProperty(ref _selectedQueueItem, value, RaiseCanExecutes); }
    public TrackListItemDto? SelectedPlaylistTrack { get => _selectedPlaylistTrack; set => SetProperty(ref _selectedPlaylistTrack, value, RaiseCanExecutes); }
    public TrackListItemDto? SelectedArtistTrack { get => _selectedArtistTrack; set => SetProperty(ref _selectedArtistTrack, value, RaiseCanExecutes); }
    public TrackListItemDto? SelectedAlbumTrack { get => _selectedAlbumTrack; set => SetProperty(ref _selectedAlbumTrack, value, RaiseCanExecutes); }
    public AlbumListItemDto? SelectedArtistAlbum { get => _selectedArtistAlbum; set => SetProperty(ref _selectedArtistAlbum, value, RaiseCanExecutes); }

    public PlaylistListItemDto? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set
        {
            if (!SetProperty(ref _selectedPlaylist, value, RaiseCanExecutes))
                return;
            if (value is not null && ActiveSection != "playlists")
                ActiveSection = "playlists";
            _ = LoadPlaylistTracksAsync();
        }
    }

    public TrackListItemDto? CurrentTrack { get => _currentTrack; private set => SetProperty(ref _currentTrack, value, RaiseCanExecutes); }
    public SubscriptionPlanDto? SelectedSubscriptionPlan
    {
        get => _selectedSubscriptionPlan;
        set => SetProperty(ref _selectedSubscriptionPlan, value, RaiseCanExecutes);
    }
    public SubscriptionPlanDto? FreePlan { get => _freePlan; private set => SetProperty(ref _freePlan, value); }
    public SubscriptionPlanDto? StudentPlan { get => _studentPlan; private set => SetProperty(ref _studentPlan, value); }
    public SubscriptionPlanDto? PremiumPlan { get => _premiumPlan; private set => SetProperty(ref _premiumPlan, value); }
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value))
                return;

            OnPropertyChanged(nameof(UserInitial));
        }
    }
    public string RoleTitle
    {
        get => _roleTitle;
        set
        {
            if (!SetProperty(ref _roleTitle, value))
                return;

            OnPropertyChanged(nameof(IsArtistOrAdmin));
        }
    }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string? UserAvatarSource
    {
        get => _userAvatarSource;
        set
        {
            if (!SetProperty(ref _userAvatarSource, value))
                return;

            OnPropertyChanged(nameof(HasUserAvatar));
            OnPropertyChanged(nameof(ShowAvatarPlaceholder));
        }
    }

    public bool HasUserAvatar => !string.IsNullOrWhiteSpace(UserAvatarSource);
    public bool ShowAvatarPlaceholder => !HasUserAvatar;
    public string UserInitial => string.IsNullOrWhiteSpace(DisplayName) ? "Y" : DisplayName.Trim()[0].ToString().ToUpperInvariant();
    public bool IsArtistOrAdmin => RoleTitle.Equals("Artist", StringComparison.OrdinalIgnoreCase) || RoleTitle.Equals("Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value, RaiseCanExecutes); }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (!SetProperty(ref _selectedGenre, value)) return;
            if (!_isInitializing && !IsBusy) _ = LoadTracksAsync();
        }
    }
    public string SelectedSearchType
    {
        get => _selectedSearchType;
        set
        {
            if (!SetProperty(ref _selectedSearchType, value))
                return;

            NotifySearchType();
        }
    }
    public string NewPlaylistTitle { get => _newPlaylistTitle; set => SetProperty(ref _newPlaylistTitle, value, RaiseCanExecutes); }
    public string NewPlaylistDescription { get => _newPlaylistDescription; set => SetProperty(ref _newPlaylistDescription, value); }
    public bool NewPlaylistIsPublic { get => _newPlaylistIsPublic; set => SetProperty(ref _newPlaylistIsPublic, value); }
    public bool IsPlaylistModalOpen { get => _isPlaylistModalOpen; set => SetProperty(ref _isPlaylistModalOpen, value); }
    public bool IsPlaylistEditMode { get => _isPlaylistEditMode; set => SetProperty(ref _isPlaylistEditMode, value); }

    public bool IsShuffleEnabled { get => _isShuffleEnabled; set { if (SetProperty(ref _isShuffleEnabled, value)) { OnPropertyChanged(nameof(ShuffleLabel)); OnPropertyChanged(nameof(IsRepeatOrShuffleActive)); } } }
    public PlaybackMode PlaybackMode { get => _playbackMode; set { if (SetProperty(ref _playbackMode, value)) { OnPropertyChanged(nameof(RepeatLabel)); OnPropertyChanged(nameof(IsRepeatEnabled)); OnPropertyChanged(nameof(RepeatGlyph)); OnPropertyChanged(nameof(IsRepeatOrShuffleActive)); } } }
    public bool IsQueuePanelOpen { get => _isQueuePanelOpen; set => SetProperty(ref _isQueuePanelOpen, value); }
    public bool IsOverviewOpen
    {
        get => _isOverviewOpen;
        set
        {
            if (!SetProperty(ref _isOverviewOpen, value))
                return;
            OnPropertyChanged(nameof(IsHomeFeedVisible));
        }
    }
    public bool IsHomeFeedVisible => !IsOverviewOpen;
    public string ShuffleLabel => IsShuffleEnabled ? "Shuffle On" : "Shuffle";
    public string RepeatLabel => PlaybackMode switch { PlaybackMode.RepeatAll => "Repeat All", PlaybackMode.RepeatOne => "Repeat One", _ => "Repeat" };
    public bool IsRepeatEnabled => PlaybackMode != PlaybackMode.Normal;
    public bool IsRepeatOrShuffleActive => IsShuffleEnabled || IsRepeatEnabled;
    public string RepeatGlyph => PlaybackMode == PlaybackMode.RepeatOne ? "🔂" : "🔁";

    public string ActiveSection { get => _activeSection; set { if (SetProperty(ref _activeSection, value)) NotifySections(); } }
    public bool IsTracksSection => ActiveSection == "tracks";
    public bool IsSearchSection => ActiveSection == "search";
    public bool IsPremiumSection => ActiveSection == "premium";
    public bool IsLikedSection => ActiveSection == "liked";
    public bool IsQueueSection => ActiveSection == "queue";
    public bool IsPlaylistsSection => ActiveSection == "playlists";
    public bool IsArtistSection => ActiveSection == "artist";
    public bool IsAlbumSection => ActiveSection == "album";
    public bool IsSearchAllType => SelectedSearchType == "Все";
    public bool IsSearchArtistsType => SelectedSearchType == "Исполнители";
    public bool IsSearchTracksType => SelectedSearchType == "Треки";
    public bool IsSearchAlbumsType => SelectedSearchType == "Альбомы";
    public bool IsSearchPlaylistsType => SelectedSearchType == "Плейлисты";

    public string ArtistHeader { get => _artistHeader; set => SetProperty(ref _artistHeader, value); }
    public string AlbumHeader { get => _albumHeader; set => SetProperty(ref _albumHeader, value); }
    public string ArtistHeroCoverPath { get => _artistHeroCoverPath; set => SetProperty(ref _artistHeroCoverPath, value); }
    public string AlbumCoverPath { get => _albumCoverPath; set => SetProperty(ref _albumCoverPath, value); }

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (!SetProperty(ref _volumePercent, v)) return;
            _audioPlayer.Volume = v / 100d;
            if (v > 0 && IsMuted) IsMuted = false;
            _ = SaveSettingsAsync();
            OnPropertyChanged(nameof(VolumeLabel));
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetProperty(ref _isMuted, value)) return;
            _audioPlayer.Volume = value ? 0d : VolumePercent / 100d;
            _ = SaveSettingsAsync();
            OnPropertyChanged(nameof(VolumeLabel));
        }
    }

    public string VolumeLabel => IsMuted ? "Mute" : $"{VolumePercent}%";
    public double PositionSeconds { get => _positionSeconds; set { if (SetProperty(ref _positionSeconds, value) && !_isSeeking) _audioPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0, value))); } }
    public double DurationSeconds { get => _durationSeconds; set => SetProperty(ref _durationSeconds, value); }
    public string PositionText => TimeSpan.FromSeconds(Math.Max(PositionSeconds, 0)).ToString(@"mm\:ss");
    public string DurationText => TimeSpan.FromSeconds(Math.Max(DurationSeconds, 0)).ToString(@"mm\:ss");
    public string SeekPreviewText => TimeSpan.FromSeconds(Math.Max(_seekPreviewSeconds, 0)).ToString(@"mm\:ss");
    public bool IsSeekPreviewVisible => _isSeekPreviewVisible;
    public string CurrentTrackTitle => CurrentTrack?.Title ?? "Трек не выбран";
    public string CurrentTrackArtist => CurrentTrack?.Artist ?? string.Empty;
    public string? CurrentTrackCoverSource => CurrentTrack?.CoverSource;
    public string CurrentArtistMonthlyListenersText => $"{EstimateMonthlyListeners((CurrentTrack ?? SelectedTrack)?.ArtistUserId ?? 0):N0} слушателей в месяц";
    public string PlaylistModalHeader => IsPlaylistEditMode ? "Редактировать плейлист" : "Создать плейлист";
    public string PlaylistSubmitText => IsPlaylistEditMode ? "Сохранить" : "Создать";
    public string FoundTracksText => $"Найдено: {Tracks.Count}";
    public string LikeButtonText => SelectedTrack is not null && _likedSongIds.Contains(SelectedTrack.Id) ? "Убрать лайк" : "Лайк";
    public bool CanMoveQueueUp => SelectedQueueItem is not null && QueueItems.Count > 1 && SelectedQueueItem.Position > 1;
    public bool CanMoveQueueDown => SelectedQueueItem is not null && QueueItems.Count > 1 && SelectedQueueItem.Position < QueueItems.Count;

    public AsyncRelayCommand RefreshTracksCommand { get; }
    public AsyncRelayCommand SearchTracksCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }
    public AsyncRelayCommand LikeSelectedTrackCommand { get; }
    public AsyncRelayCommand AddToQueueCommand { get; }
    public AsyncRelayCommand RemoveFromQueueCommand { get; }
    public AsyncRelayCommand MoveQueueUpCommand { get; }
    public AsyncRelayCommand MoveQueueDownCommand { get; }
    public AsyncRelayCommand ClearQueueCommand { get; }
    public AsyncRelayCommand CreatePlaylistCommand { get; }
    public AsyncRelayCommand SavePlaylistModalCommand { get; }
    public AsyncRelayCommand DeletePlaylistCommand { get; }
    public AsyncRelayCommand AddSelectedTrackToPlaylistCommand { get; }
    public AsyncRelayCommand RemovePlaylistTrackCommand { get; }
    public AsyncRelayCommand OpenSelectedArtistCommand { get; }
    public AsyncRelayCommand OpenSelectedAlbumCommand { get; }
    public AsyncRelayCommand OpenArtistAlbumCommand { get; }
    public AsyncRelayCommand PlaySelectedTrackCommand { get; }
    public AsyncRelayCommand PlayLikedTrackCommand { get; }
    public AsyncRelayCommand PlayQueueTrackCommand { get; }
    public AsyncRelayCommand PlayPlaylistTrackCommand { get; }
    public AsyncRelayCommand PlayArtistTrackCommand { get; }
    public AsyncRelayCommand PlayAlbumTrackCommand { get; }

    public RelayCommand SetSectionTracksCommand { get; }
    public RelayCommand SetSectionBrowseCommand { get; }
    public RelayCommand SetSectionPremiumCommand { get; }
    public RelayCommand SetSectionLikedCommand { get; }
    public RelayCommand SetSectionQueueCommand { get; }
    public RelayCommand ToggleQueuePanelCommand { get; }
    public RelayCommand SetSectionPlaylistsCommand { get; }
    public RelayCommand OpenCreatePlaylistModalCommand { get; }
    public RelayCommand OpenEditPlaylistModalCommand { get; }
    public RelayCommand ClosePlaylistModalCommand { get; }
    public RelayCommand SetSectionArtistCommand { get; }
    public RelayCommand SetSectionAlbumCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand NextTrackCommand { get; }
    public RelayCommand PreviousTrackCommand { get; }
    public RelayCommand MuteCommand { get; }
    public RelayCommand ToggleShuffleCommand { get; }
    public RelayCommand ToggleRepeatModeCommand { get; }
    public AsyncRelayCommand AddCurrentTrackToLikedCommand { get; }
    public AsyncRelayCommand AddCurrentTrackToPlaylistCommand { get; }
    private async Task InitializeAsync()
    {
        await LoadProfileAsync();

        var settings = await _playerSettingsStore.LoadAsync();
        VolumePercent = (int)Math.Round(Math.Clamp(settings.Volume, 0.0, 1.0) * 100);
        IsMuted = settings.IsMuted;

        ApplyFixedHomeCategories();
        await LoadTracksAsync();
        await LoadLikedAsync();
        await LoadQueueAsync();
        await LoadPlaylistsAsync();
        await LoadSubscriptionPlansAsync();
        _isInitializing = false;
    }


    private async Task LoadProfileAsync()
    {
        var (profile, error) = await _authSessionService.ApiClient.GetProfileMeAsync();
        if (!string.IsNullOrWhiteSpace(error) || profile is null)
            return;

        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? DisplayName : profile.DisplayName;
        RoleTitle = string.IsNullOrWhiteSpace(profile.RoleTitle) ? RoleTitle : profile.RoleTitle;
        UserAvatarSource = _authSessionService.ApiClient.ResolveAssetUrl(profile.AvatarPath);
    }
    private void ApplyFixedHomeCategories()
    {
        GenreOptions.Clear();
        GenreOptions.Add("Все");
        GenreOptions.Add("Музыка");
        GenreOptions.Add("Подкасты");
        GenreOptions.Add("Аудиокниги");
        if (!GenreOptions.Contains(SelectedGenre))
            SelectedGenre = "Все";
    }

    private async Task LoadTracksAsync()
    {
        IsBusy = true;
        try
        {
            var (items, error) = await _authSessionService.ApiClient.GetTracksAsync(SearchText, SelectedGenre, "title");
            Tracks.Clear();
            foreach (var item in items) Tracks.Add(item);

            Status = string.IsNullOrWhiteSpace(error)
                ? "Треки обновлены."
                : $"Ошибка треков: {error}";

            BuildSearchResults();
            OnPropertyChanged(nameof(FoundTracksText));
            if (Tracks.Count > 0 && SelectedTrack is null) SelectedTrack = Tracks[0];
            SeedRecentTracks();
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

    private void BuildSearchResults()
    {
        SearchResultTracks.Clear();
        SearchResultArtists.Clear();
        SearchResultAlbums.Clear();
        SearchResultPlaylists.Clear();

        foreach (var track in Tracks)
            SearchResultTracks.Add(track);

        foreach (var artist in Tracks
                     .Select(t => t.Artist)
                     .Where(a => !string.IsNullOrWhiteSpace(a))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SearchResultArtists.Add(artist);
        }

        foreach (var album in Tracks
                     .Where(t => t.AlbumId.HasValue)
                     .Select(t => $"Альбом #{t.AlbumId}")
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SearchResultAlbums.Add(album);
        }

        var needle = SearchText?.Trim() ?? string.Empty;
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

    private async Task LoadLikedAsync()
    {
        var (items, error) = await _authSessionService.ApiClient.GetLikedTracksAsync();
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка лайков: {error}"; return; }

        LikedTracks.Clear();
        _likedSongIds.Clear();
        foreach (var item in items) { LikedTracks.Add(item); _likedSongIds.Add(item.Id); }
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
        BuildSearchResults();
    }

    private async Task LoadPlaylistTracksAsync()
    {
        PlaylistTracks.Clear();
        if (SelectedPlaylist is null) return;

        var (items, error) = await _authSessionService.ApiClient.GetPlaylistTracksAsync(SelectedPlaylist.Id);
        if (!string.IsNullOrWhiteSpace(error)) { Status = $"Ошибка треков плейлиста: {error}"; return; }

        foreach (var item in items) PlaylistTracks.Add(item);
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
        var (artist, error) = await _authSessionService.ApiClient.GetArtistAsync(src.ArtistUserId);
        if (!string.IsNullOrWhiteSpace(error) || artist is null) { Status = $"Ошибка артиста: {error}"; return; }

        ArtistHeader = artist.ArtistName;
        ArtistTopTracks.Clear();
        ArtistAlbums.Clear();
        foreach (var t in artist.TopTracks) ArtistTopTracks.Add(t);
        foreach (var a in artist.Albums) ArtistAlbums.Add(a);
        ArtistHeroCoverPath = artist.Albums.FirstOrDefault()?.CoverSource
            ?? artist.TopTracks.FirstOrDefault()?.CoverSource
            ?? string.Empty;
        ActiveSection = "artist";
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
        AlbumCoverPath = album.CoverPath ?? string.Empty;
        AlbumTracks.Clear();
        foreach (var t in album.Tracks) AlbumTracks.Add(t);
        ActiveSection = "album";
    }

    private async Task PlayTrackAsync(TrackListItemDto? track)
    {
        if (track is null) return;
        if (string.IsNullOrWhiteSpace(track.Source)) { Status = "У трека нет Source."; return; }

        try
        {
            _audioPlayer.Load(track.Source);
            _audioPlayer.Volume = IsMuted ? 0d : VolumePercent / 100d;
            _audioPlayer.Play();
            CurrentTrack = track;
            SelectedTrack = track;
            RememberTrack(track);
            Status = $"Сейчас играет: {track.Title}";
            UpdateTime();
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
            return;
        }

        if (Tracks.Count == 0) return;

        TrackListItemDto? next = IsShuffleEnabled ? Tracks[_random.Next(Tracks.Count)] : NextFromTracks();
        if (next is null)
        {
            _audioPlayer.Stop();
            Status = "Конец списка треков.";
            return;
        }

        await PlayTrackAsync(next);
    }

    private TrackListItemDto? NextFromTracks()
    {
        if (CurrentTrack is null) return Tracks[0];
        var idx = Tracks.IndexOf(CurrentTrack) + 1;
        if (idx >= Tracks.Count)
            return PlaybackMode == PlaybackMode.RepeatAll ? Tracks[0] : null;
        return Tracks[idx];
    }

    private void PlayPreviousTrack()
    {
        if (Tracks.Count == 0) return;
        if (CurrentTrack is null) { _ = PlayTrackAsync(Tracks[0]); return; }
        var idx = Tracks.IndexOf(CurrentTrack) - 1;
        if (idx < 0) idx = PlaybackMode == PlaybackMode.RepeatAll ? Tracks.Count - 1 : 0;
        _ = PlayTrackAsync(Tracks[idx]);
    }

    private void ToggleRepeatMode() => PlaybackMode = PlaybackMode switch { PlaybackMode.Normal => PlaybackMode.RepeatAll, PlaybackMode.RepeatAll => PlaybackMode.RepeatOne, _ => PlaybackMode.Normal };

    private async Task LogoutAsync()
    {
        _audioPlayer.Stop();
        var session = await _authSessionService.SessionStore.TryLoadAsync();
        if (session is not null && !string.IsNullOrWhiteSpace(session.RefreshToken))
            await _authSessionService.ApiClient.LogoutAsync(session.RefreshToken);

        await _authSessionService.SessionStore.ClearAsync();
        _authSessionService.ApiClient.SetAccessToken(null);
        await _onLogout();
    }

    private async Task SaveSettingsAsync() => await _playerSettingsStore.SaveAsync(new PlayerSettingsSnapshot { Volume = VolumePercent / 100d, IsMuted = IsMuted });

    private void UpdatePlayback()
    {
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentTrackArtist));
        OnPropertyChanged(nameof(CurrentTrackCoverSource));
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
        OnPropertyChanged(nameof(LikeButtonText));
        OnPropertyChanged(nameof(CanMoveQueueUp));
        OnPropertyChanged(nameof(CanMoveQueueDown));
        OnPropertyChanged(nameof(CurrentArtistMonthlyListenersText));
    }

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
    }

    private void NotifySearchType()
    {
        OnPropertyChanged(nameof(IsSearchAllType));
        OnPropertyChanged(nameof(IsSearchArtistsType));
        OnPropertyChanged(nameof(IsSearchTracksType));
        OnPropertyChanged(nameof(IsSearchAlbumsType));
        OnPropertyChanged(nameof(IsSearchPlaylistsType));
    }

    private void SeedRecentTracks()
    {
        if (RecentTracks.Count > 0)
            return;

        foreach (var track in Tracks.Take(8))
            RecentTracks.Add(track);
    }

    private void RememberTrack(TrackListItemDto track)
    {
        var existing = RecentTracks.FirstOrDefault(x => x.Id == track.Id);
        if (existing is not null)
            RecentTracks.Remove(existing);
        RecentTracks.Insert(0, track);
        while (RecentTracks.Count > 12)
            RecentTracks.RemoveAt(RecentTracks.Count - 1);
    }

    private static int EstimateMonthlyListeners(int artistUserId)
    {
        if (artistUserId <= 0)
            return 125000;

        return 45000 + (int)((artistUserId * 7919L) % 920000L);
    }

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

    public void HideSeekPreview()
    {
        if (!_isSeekPreviewVisible) return;
        _isSeekPreviewVisible = false;
        OnPropertyChanged(nameof(IsSeekPreviewVisible));
    }
}










