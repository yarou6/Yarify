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

public partial class MainWindowViewModel : BaseVM
{
    private readonly AuthSessionService _authSessionService;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly PlayerSettingsStore _playerSettingsStore;
    private readonly Func<Task> _onLogout;
    private readonly Random _random = new();
    private readonly HashSet<int> _likedSongIds = new();
    private readonly Dictionary<int, string> _albumTitleCache = new();

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
    private Bitmap? _userAvatarBitmap;
    private string _roleTitle = "User";
    private string? _artistName;
    private string _profileLogin = string.Empty;
    private string _profileEmail = string.Empty;
    private string? _profilePhone;
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
    private string _newPlaylistCoverPath = string.Empty;
    private bool _newPlaylistIsPublic;
    private bool _isPlaylistModalOpen;
    private bool _isPlaylistEditMode;
    private bool _allowExplicitContent = true;
    private int _followingArtistsCount;
    private UserSubscriptionDto? _currentSubscription;
    private string _settingsArtistNameInput = string.Empty;
    private string _editDisplayName = string.Empty;
    private string _editAvatarPath = string.Empty;
    private bool _isEditProfileModalOpen;
    private bool _isEditContactsModalOpen;
    private string _editEmailInput = string.Empty;
    private string _editPhoneInput = string.Empty;
    private bool _isAddTrackModalOpen;
    private bool _isAlbumTrackMode;
    private string _addTrackAlbumTitle = string.Empty;
    private string _addTrackPlannedCountInput = "1";
    private string _addTrackTitleInput = string.Empty;
    private string _addTrackDurationInput = "180";
    private string _addTrackGenreSearchInput = string.Empty;
    private bool _addTrackIsOnlineSource;
    private string _addTrackLocalPath = string.Empty;
    private string _addTrackStreamUrl = string.Empty;
    private string _addTrackAlbumCoverPath = string.Empty;
    private string _addTrackCoverPath = string.Empty;
    private bool _addTrackExplicit;
    private int _albumTracksRemaining;
    private int _albumTracksTotal;
    private int? _draftAlbumId;
    private string _currentPasswordInput = string.Empty;
    private string _newPasswordInput = string.Empty;
    private string _confirmPasswordInput = string.Empty;
    private string _settingsLanguage = "Русский (Russian)";
    private bool _isContactsVisible;

    private bool _isSeekPreviewVisible;
    private double _seekPreviewSeconds;

    private string _artistHeroCoverPath = string.Empty;
    private string _artistAvatarPath = string.Empty;
    private Bitmap? _artistAvatarBitmap;
    private int _currentArtistUserId;
    private int _artistMonthlyStreams;
    private int _artistFollowersCount;
    private bool _isFollowingArtist;
    private string _artistReleaseFilter = "all";
    private bool _isArtistReleasesModalOpen;
    private string _albumCoverPath = string.Empty;
    private Bitmap? _albumCoverBitmap;
    private long? _activeListeningEventId;
    private int _activeListeningSongId;
    private DateTime _lastListeningProgressSentAt = DateTime.MinValue;
    private bool _isAdvancingTrack;
    private string _playbackContextKey = "tracks";
    private int _currentArtistPlaysTotal;

    private bool _isShuffleEnabled;
    private bool _isQueuePanelOpen = true;
    private PlaybackMode _playbackMode = PlaybackMode.Normal;
    private string _activeSection = "tracks";
    private string _artistHeader = "Артист";
    private string _albumHeader = "Альбом";
    private bool _isOverviewOpen;
    private string _albumTitleText = "Альбом";
    private string _albumArtistNameText = string.Empty;
    private string _albumMetaText = string.Empty;
    private string _playlistTitleText = "Плейлист";
    private string _playlistMetaText = string.Empty;
    private string _playlistCoverPath = string.Empty;
    private Bitmap? _playlistCoverBitmap;
    private SubscriptionPlanDto? _selectedSubscriptionPlan;
    private SubscriptionPlanDto? _freePlan;
    private SubscriptionPlanDto? _studentPlan;
    private SubscriptionPlanDto? _premiumPlan;

    public MainWindowViewModel(AuthSessionService authSessionService, AuthResponseDto authData,
        IAudioPlayerService audioPlayer, PlayerSettingsStore playerSettingsStore, Func<Task> onLogout)
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
        ArtistReleases = new ObservableCollection<ArtistReleaseItemDto>();
        AlbumTracks = new ObservableCollection<TrackListItemDto>();
        RecentTracks = new ObservableCollection<TrackListItemDto>();
        HomeLikedRecentTracks = new ObservableCollection<TrackListItemDto>();
        HomeRecentCollections = new ObservableCollection<HomeMediaCollectionItemDto>();
        ForYouTracks = new ObservableCollection<TrackListItemDto>();
        HomeRecommendedAlbums = new ObservableCollection<AlbumListItemDto>();
        SubscriptionPlans = new ObservableCollection<SubscriptionPlanDto>();
        SearchResultTracks = new ObservableCollection<TrackListItemDto>();
        SearchResultArtists = new ObservableCollection<ArtistSearchItemDto>();
        SearchResultAlbums = new ObservableCollection<AlbumListItemDto>();
        SearchResultPlaylists = new ObservableCollection<PlaylistListItemDto>();
        AddTrackGenres = new ObservableCollection<AddTrackGenreItemViewModel>();
        FilteredAddTrackGenres = new ObservableCollection<AddTrackGenreItemViewModel>();

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

        CreatePlaylistCommand =
            new AsyncRelayCommand(CreatePlaylistAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        SavePlaylistModalCommand =
            new AsyncRelayCommand(SavePlaylistModalAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        OpenCreatePlaylistModalCommand = new RelayCommand(OpenCreatePlaylistModal);
        OpenEditPlaylistModalCommand = new RelayCommand(OpenEditPlaylistModal, () => SelectedPlaylist is not null);
        ClosePlaylistModalCommand = new RelayCommand(() => IsPlaylistModalOpen = false);
        DeletePlaylistCommand = new AsyncRelayCommand(DeleteSelectedPlaylistAsync, () => SelectedPlaylist is not null);
        AddSelectedTrackToPlaylistCommand = new AsyncRelayCommand(AddSelectedTrackToPlaylistAsync,
            () => SelectedTrack is not null && SelectedPlaylist is not null);
        RemovePlaylistTrackCommand = new AsyncRelayCommand(RemoveSelectedPlaylistTrackAsync,
            () => SelectedPlaylistTrack is not null && SelectedPlaylist is not null);

        OpenSelectedArtistCommand =
            new AsyncRelayCommand(OpenSelectedArtistAsync, () => (SelectedTrack ?? CurrentTrack) is not null);
        OpenAlbumArtistCommand = new AsyncRelayCommand(OpenAlbumArtistAsync,
            () => (SelectedAlbumTrack?.ArtistUserId ?? _currentArtistUserId) > 0);
        ToggleArtistFollowCommand = new AsyncRelayCommand(ToggleArtistFollowAsync, () => _currentArtistUserId > 0);
        SetArtistReleaseAllCommand = new RelayCommand(() => SetArtistReleaseFilter("all"));
        SetArtistReleaseAlbumCommand = new RelayCommand(() => SetArtistReleaseFilter("album"));
        SetArtistReleaseSingleCommand = new RelayCommand(() => SetArtistReleaseFilter("single"));
        ShowAllArtistReleasesCommand = new RelayCommand(OpenArtistReleasesModal);
        CloseArtistReleasesModalCommand = new RelayCommand(() => IsArtistReleasesModalOpen = false);
        OpenSelectedAlbumCommand = new AsyncRelayCommand(OpenSelectedAlbumAsync,
            () => (SelectedTrack?.AlbumId ?? CurrentTrack?.AlbumId) is not null);
        OpenArtistAlbumCommand =
            new AsyncRelayCommand(OpenSelectedArtistAlbumAsync, () => SelectedArtistAlbum is not null);

        PlaySelectedTrackCommand = new AsyncRelayCommand(async () => await PlayFromTracksAsync(SelectedTrack),
            () => SelectedTrack is not null);
        PlayLikedTrackCommand = new AsyncRelayCommand(async () => await PlayFromLikedAsync(SelectedLikedTrack),
            () => SelectedLikedTrack is not null);
        PlayQueueTrackCommand = new AsyncRelayCommand(async () => await PlayFromQueueAsync(SelectedQueueItem?.Track),
            () => SelectedQueueItem is not null);
        PlayPlaylistTrackCommand = new AsyncRelayCommand(async () => await PlayFromPlaylistAsync(SelectedPlaylistTrack),
            () => SelectedPlaylistTrack is not null);
        PlayArtistTrackCommand = new AsyncRelayCommand(async () => await PlayFromArtistAsync(SelectedArtistTrack),
            () => SelectedArtistTrack is not null);
        PlayAlbumTrackCommand = new AsyncRelayCommand(PlayAlbumPrimaryAsync,
            () => SelectedAlbumTrack is not null || AlbumTracks.Count > 0);

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
        SetSectionProfileCommand = new RelayCommand(() => ActiveSection = "profile");
        SetSectionSettingsCommand = new RelayCommand(() => ActiveSection = "settings");
        ToggleQueuePanelCommand = new RelayCommand(() => IsQueuePanelOpen = !IsQueuePanelOpen);
        SetSectionPlaylistsCommand = new RelayCommand(() => ActiveSection = "playlists");
        SetSectionArtistCommand = new RelayCommand(() => ActiveSection = "artist");
        SetSectionAlbumCommand = new RelayCommand(() => ActiveSection = "album");
        OpenEditProfileModalCommand = new RelayCommand(() =>
        {
            EditDisplayName = DisplayName;
            EditAvatarPath = string.Empty;
            IsEditProfileModalOpen = true;
        });
        CloseEditProfileModalCommand = new RelayCommand(() => IsEditProfileModalOpen = false);
        OpenEditContactsModalCommand = new RelayCommand(() =>
        {
            EditEmailInput = ProfileEmail;
            EditPhoneInput = ProfilePhone ?? string.Empty;
            IsEditContactsModalOpen = true;
        });
        CloseEditContactsModalCommand = new RelayCommand(() => IsEditContactsModalOpen = false);
        SaveContactsCommand = new AsyncRelayCommand(SaveContactsAsync, CanSaveContacts);
        CloseAddTrackModalCommand = new RelayCommand(CloseAddTrackModal);
        SubmitAddTrackCommand = new AsyncRelayCommand(SubmitAddTrackAsync, CanSubmitAddTrack);
        AddCurrentTrackToLikedCommand =
            new AsyncRelayCommand(AddCurrentTrackToLikedAsync, () => CurrentTrack is not null);
        AddCurrentTrackToPlaylistCommand =
            new AsyncRelayCommand(AddCurrentTrackToPlaylistAsync, () => CurrentTrack is not null);
        AddTrackCommand = new RelayCommand(AddTrackAction);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);
        SaveArtistNameCommand = new AsyncRelayCommand(SaveArtistNameAsync, CanSaveArtistName);
        SaveProfileChangesCommand = new AsyncRelayCommand(SaveProfileChangesAsync, CanSaveProfileChanges);
        SelectFreePlanCommand = new AsyncRelayCommand(() => SelectPlanAsync(FreePlan), () => CanSelectPlan(FreePlan));
        SelectStudentPlanCommand =
            new AsyncRelayCommand(() => SelectPlanAsync(StudentPlan), () => CanSelectPlan(StudentPlan));
        SelectPremiumPlanCommand =
            new AsyncRelayCommand(() => SelectPlanAsync(PremiumPlan), () => CanSelectPlan(PremiumPlan));

        PlayPauseCommand = new RelayCommand(PlayPause, () => CurrentTrack is not null);
        NextTrackCommand = new RelayCommand(() => _ = PlayNextTrackAsync());
        PreviousTrackCommand = new RelayCommand(PlayPreviousTrack);
        MuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        ToggleShuffleCommand = new RelayCommand(() => IsShuffleEnabled = !IsShuffleEnabled);
        ToggleRepeatModeCommand = new RelayCommand(ToggleRepeatMode);
        ToggleContactsVisibilityCommand = new RelayCommand(() => IsContactsVisible = !IsContactsVisible);

        _audioPlayer.PlaybackStateChanged += (_, _) => Dispatcher.UIThread.Post(UpdatePlayback);
        _audioPlayer.PositionChanged += (_, _) => Dispatcher.UIThread.Post(UpdateTime);
        _audioPlayer.TrackEnded += (_, _) => Dispatcher.UIThread.Post(async () => await HandleTrackEndedAsync());

        _ = InitializeAsync();
    }

    public ObservableCollection<TrackListItemDto> Tracks { get; }
    public ObservableCollection<TrackListItemDto> LikedTracks { get; }
    public ObservableCollection<QueueItemDto> QueueItems { get; }
    public ObservableCollection<PlaylistListItemDto> Playlists { get; }
    public ObservableCollection<TrackListItemDto> PlaylistTracks { get; }
    public ObservableCollection<TrackListItemDto> ArtistTopTracks { get; }
    public ObservableCollection<AlbumListItemDto> ArtistAlbums { get; }
    public ObservableCollection<ArtistReleaseItemDto> ArtistReleases { get; }
    public ObservableCollection<TrackListItemDto> AlbumTracks { get; }
    public ObservableCollection<TrackListItemDto> RecentTracks { get; }
    public ObservableCollection<TrackListItemDto> HomeLikedRecentTracks { get; }
    public ObservableCollection<HomeMediaCollectionItemDto> HomeRecentCollections { get; }
    public ObservableCollection<TrackListItemDto> ForYouTracks { get; }
    public ObservableCollection<AlbumListItemDto> HomeRecommendedAlbums { get; }
    public ObservableCollection<string> GenreOptions { get; }
    public ObservableCollection<SubscriptionPlanDto> SubscriptionPlans { get; }
    public ObservableCollection<TrackListItemDto> SearchResultTracks { get; }
    public ObservableCollection<ArtistSearchItemDto> SearchResultArtists { get; }
    public ObservableCollection<AlbumListItemDto> SearchResultAlbums { get; }
    public ObservableCollection<PlaylistListItemDto> SearchResultPlaylists { get; }
    public ObservableCollection<AddTrackGenreItemViewModel> AddTrackGenres { get; }
    public ObservableCollection<AddTrackGenreItemViewModel> FilteredAddTrackGenres { get; }
    public ObservableCollection<string> SearchTypeOptions { get; }

    public TrackListItemDto? SelectedTrack
    {
        get => _selectedTrack;
        set => SetProperty(ref _selectedTrack, value, RaiseCanExecutes);
    }

    public TrackListItemDto? SelectedLikedTrack
    {
        get => _selectedLikedTrack;
        set => SetProperty(ref _selectedLikedTrack, value, RaiseCanExecutes);
    }

    public QueueItemDto? SelectedQueueItem
    {
        get => _selectedQueueItem;
        set => SetProperty(ref _selectedQueueItem, value, RaiseCanExecutes);
    }

    public TrackListItemDto? SelectedPlaylistTrack
    {
        get => _selectedPlaylistTrack;
        set => SetProperty(ref _selectedPlaylistTrack, value, RaiseCanExecutes);
    }

    public TrackListItemDto? SelectedArtistTrack
    {
        get => _selectedArtistTrack;
        set => SetProperty(ref _selectedArtistTrack, value, RaiseCanExecutes);
    }

    public TrackListItemDto? SelectedAlbumTrack
    {
        get => _selectedAlbumTrack;
        set => SetProperty(ref _selectedAlbumTrack, value, RaiseCanExecutes);
    }

    public AlbumListItemDto? SelectedArtistAlbum
    {
        get => _selectedArtistAlbum;
        set => SetProperty(ref _selectedArtistAlbum, value, RaiseCanExecutes);
    }

    public PlaylistListItemDto? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set
        {
            if (!SetProperty(ref _selectedPlaylist, value, RaiseCanExecutes))
                return;
            UpdatePlaylistHeaderFromSelection();
            _ = LoadPlaylistTracksAsync();
        }
    }

    public PlaylistListItemDto? SidebarSelectedPlaylist
    {
        get => IsPlaylistsSection ? SelectedPlaylist : null;
        set
        {
            if (value is null)
                return;
            SelectedPlaylist = value;
            ActiveSection = "playlists";
        }
    }

    public TrackListItemDto? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (!SetProperty(ref _currentTrack, value, RaiseCanExecutes))
                return;
            OnPropertyChanged(nameof(AddCurrentTrackToLikedButtonText));
            UpdatePlaylistTrackPresenceFlags();
        }
    }

    public SubscriptionPlanDto? SelectedSubscriptionPlan
    {
        get => _selectedSubscriptionPlan;
        set => SetProperty(ref _selectedSubscriptionPlan, value, RaiseCanExecutes);
    }

    public SubscriptionPlanDto? FreePlan
    {
        get => _freePlan;
        private set
        {
            if (!SetProperty(ref _freePlan, value))
                return;
            OnPropertyChanged(nameof(IsFreePlanSelected));
            OnPropertyChanged(nameof(ShowFreePlanBillboard));
            OnPropertyChanged(nameof(FreePlanButtonText));
            RaiseCanExecutes();
        }
    }

    public SubscriptionPlanDto? StudentPlan
    {
        get => _studentPlan;
        private set
        {
            if (!SetProperty(ref _studentPlan, value))
                return;
            OnPropertyChanged(nameof(IsStudentPlanSelected));
            OnPropertyChanged(nameof(StudentPlanButtonText));
            RaiseCanExecutes();
        }
    }

    public SubscriptionPlanDto? PremiumPlan
    {
        get => _premiumPlan;
        private set
        {
            if (!SetProperty(ref _premiumPlan, value))
                return;
            OnPropertyChanged(nameof(IsPremiumPlanSelected));
            OnPropertyChanged(nameof(PremiumPlanButtonText));
            RaiseCanExecutes();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value))
                return;

            OnPropertyChanged(nameof(UserInitial));
            OnPropertyChanged(nameof(LikedOwnerName));
            OnPropertyChanged(nameof(LikedHeaderStats));
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

    public string? ArtistName
    {
        get => _artistName;
        set
        {
            if (!SetProperty(ref _artistName, value))
                return;

            OnPropertyChanged(nameof(HasArtistName));
            OnPropertyChanged(nameof(ProfileArtistNameText));
            OnPropertyChanged(nameof(ProfileStatsText));
            OnPropertyChanged(nameof(LikedOwnerName));
            OnPropertyChanged(nameof(LikedHeaderStats));
            RaiseCanExecutes();
        }
    }

    public string ProfileLogin
    {
        get => _profileLogin;
        private set => SetProperty(ref _profileLogin, value);
    }

    public string ProfileEmail
    {
        get => _profileEmail;
        private set
        {
            if (!SetProperty(ref _profileEmail, value))
                return;
            OnPropertyChanged(nameof(SettingsEmailText));
        }
    }

    public string? ProfilePhone
    {
        get => _profilePhone;
        private set
        {
            if (!SetProperty(ref _profilePhone, value))
                return;
            OnPropertyChanged(nameof(SettingsPhoneText));
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

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

    public Bitmap? UserAvatarBitmap
    {
        get => _userAvatarBitmap;
        private set
        {
            if (ReferenceEquals(_userAvatarBitmap, value))
                return;

            _userAvatarBitmap?.Dispose();
            _userAvatarBitmap = value;
            OnPropertyChanged(nameof(UserAvatarBitmap));
            OnPropertyChanged(nameof(HasUserAvatar));
            OnPropertyChanged(nameof(ShowAvatarPlaceholder));
        }
    }

    public bool HasUserAvatar => UserAvatarBitmap is not null;
    public bool ShowAvatarPlaceholder => !HasUserAvatar;

    public string UserInitial => string.IsNullOrWhiteSpace(DisplayName)
        ? "Y"
        : DisplayName.Trim()[0].ToString().ToUpperInvariant();

    public bool IsArtistOrAdmin => RoleTitle.Equals("Artist", StringComparison.OrdinalIgnoreCase) ||
                                   RoleTitle.Equals("Admin", StringComparison.OrdinalIgnoreCase);

    public bool HasArtistName => !string.IsNullOrWhiteSpace(ArtistName);
    public string ProfileArtistNameText => HasArtistName ? ArtistName! : "Имя артиста не задано";

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value, RaiseCanExecutes);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

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

    public string NewPlaylistTitle
    {
        get => _newPlaylistTitle;
        set => SetProperty(ref _newPlaylistTitle, value, RaiseCanExecutes);
    }

    public string NewPlaylistDescription
    {
        get => _newPlaylistDescription;
        set => SetProperty(ref _newPlaylistDescription, value);
    }

    public string NewPlaylistCoverPath
    {
        get => _newPlaylistCoverPath;
        set => SetProperty(ref _newPlaylistCoverPath, value);
    }

    public bool NewPlaylistIsPublic
    {
        get => _newPlaylistIsPublic;
        set => SetProperty(ref _newPlaylistIsPublic, value);
    }

    public bool IsPlaylistModalOpen
    {
        get => _isPlaylistModalOpen;
        set => SetProperty(ref _isPlaylistModalOpen, value);
    }

    public bool IsPlaylistEditMode
    {
        get => _isPlaylistEditMode;
        set => SetProperty(ref _isPlaylistEditMode, value);
    }

    public bool AllowExplicitContent
    {
        get => _allowExplicitContent;
        set
        {
            if (!SetProperty(ref _allowExplicitContent, value))
                return;
            _ = SaveSettingsAsync();
            _ = LoadTracksAsync();
        }
    }

    public int FollowingArtistsCount
    {
        get => _followingArtistsCount;
        private set
        {
            if (!SetProperty(ref _followingArtistsCount, value))
                return;
            OnPropertyChanged(nameof(ProfileStatsText));
        }
    }

    public UserSubscriptionDto? CurrentSubscription
    {
        get => _currentSubscription;
        private set
        {
            if (!SetProperty(ref _currentSubscription, value))
                return;
            OnPropertyChanged(nameof(CurrentSubscriptionTitle));
            OnPropertyChanged(nameof(CurrentSubscriptionAccent));
            OnPropertyChanged(nameof(IsFreePlanSelected));
            OnPropertyChanged(nameof(ShowFreePlanBillboard));
            OnPropertyChanged(nameof(IsStudentPlanSelected));
            OnPropertyChanged(nameof(IsPremiumPlanSelected));
            OnPropertyChanged(nameof(FreePlanButtonText));
            OnPropertyChanged(nameof(StudentPlanButtonText));
            OnPropertyChanged(nameof(PremiumPlanButtonText));
            RaiseCanExecutes();
        }
    }

    public string SettingsArtistNameInput
    {
        get => _settingsArtistNameInput;
        set => SetProperty(ref _settingsArtistNameInput, value, RaiseCanExecutes);
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set => SetProperty(ref _editDisplayName, value, RaiseCanExecutes);
    }

    public string EditAvatarPath
    {
        get => _editAvatarPath;
        set => SetProperty(ref _editAvatarPath, value, RaiseCanExecutes);
    }

    public bool IsEditProfileModalOpen
    {
        get => _isEditProfileModalOpen;
        set => SetProperty(ref _isEditProfileModalOpen, value);
    }

    public bool IsEditContactsModalOpen
    {
        get => _isEditContactsModalOpen;
        set => SetProperty(ref _isEditContactsModalOpen, value);
    }

    public string EditEmailInput
    {
        get => _editEmailInput;
        set => SetProperty(ref _editEmailInput, value, RaiseCanExecutes);
    }

    public string EditPhoneInput
    {
        get => _editPhoneInput;
        set => SetProperty(ref _editPhoneInput, value, RaiseCanExecutes);
    }

    public bool IsAddTrackModalOpen
    {
        get => _isAddTrackModalOpen;
        set => SetProperty(ref _isAddTrackModalOpen, value);
    }

    public bool IsAlbumTrackMode
    {
        get => _isAlbumTrackMode;
        set
        {
            if (!SetProperty(ref _isAlbumTrackMode, value))
                return;
            OnPropertyChanged(nameof(IsSingleTrackMode));
            OnPropertyChanged(nameof(AddTrackProgressText));
        }
    }

    public bool IsSingleTrackMode => !IsAlbumTrackMode;

    public string AddTrackAlbumTitle
    {
        get => _addTrackAlbumTitle;
        set => SetProperty(ref _addTrackAlbumTitle, value, RaiseCanExecutes);
    }

    public string AddTrackPlannedCountInput
    {
        get => _addTrackPlannedCountInput;
        set => SetProperty(ref _addTrackPlannedCountInput, value, RaiseCanExecutes);
    }

    public string AddTrackTitleInput
    {
        get => _addTrackTitleInput;
        set => SetProperty(ref _addTrackTitleInput, value, RaiseCanExecutes);
    }

    public string AddTrackDurationInput
    {
        get => _addTrackDurationInput;
        set => SetProperty(ref _addTrackDurationInput, value, RaiseCanExecutes);
    }

    public string AddTrackGenreSearchInput
    {
        get => _addTrackGenreSearchInput;
        set
        {
            if (!SetProperty(ref _addTrackGenreSearchInput, value))
                return;
            RefreshFilteredAddTrackGenres();
        }
    }

    public bool AddTrackIsOnlineSource
    {
        get => _addTrackIsOnlineSource;
        set
        {
            if (!SetProperty(ref _addTrackIsOnlineSource, value))
                return;
            OnPropertyChanged(nameof(IsLocalTrackSource));
            OnPropertyChanged(nameof(IsOnlineTrackSource));
            if (!value)
                TryApplyDurationFromLocalAudio(AddTrackLocalPath);
        }
    }

    public bool IsLocalTrackSource => !AddTrackIsOnlineSource;
    public bool IsOnlineTrackSource => AddTrackIsOnlineSource;

    public string AddTrackLocalPath
    {
        get => _addTrackLocalPath;
        set
        {
            if (!SetProperty(ref _addTrackLocalPath, value))
                return;
            TryApplyDurationFromLocalAudio(value);
        }
    }

    public string AddTrackStreamUrl
    {
        get => _addTrackStreamUrl;
        set => SetProperty(ref _addTrackStreamUrl, value);
    }

    public string AddTrackAlbumCoverPath
    {
        get => _addTrackAlbumCoverPath;
        set => SetProperty(ref _addTrackAlbumCoverPath, value);
    }

    public string AddTrackCoverPath
    {
        get => _addTrackCoverPath;
        set => SetProperty(ref _addTrackCoverPath, value);
    }

    public bool AddTrackExplicit
    {
        get => _addTrackExplicit;
        set => SetProperty(ref _addTrackExplicit, value);
    }

    public string AddTrackProgressText => IsAlbumTrackMode
        ? (_albumTracksRemaining > 0
            ? $"Осталось добавить треков: {_albumTracksRemaining}"
            : "Режим альбома: укажи название и количество треков.")
        : "Режим сингла: можно добавлять треки по одному.";

    public string CurrentPasswordInput
    {
        get => _currentPasswordInput;
        set => SetProperty(ref _currentPasswordInput, value, RaiseCanExecutes);
    }

    public string NewPasswordInput
    {
        get => _newPasswordInput;
        set => SetProperty(ref _newPasswordInput, value, RaiseCanExecutes);
    }

    public string ConfirmPasswordInput
    {
        get => _confirmPasswordInput;
        set => SetProperty(ref _confirmPasswordInput, value, RaiseCanExecutes);
    }

    public string SettingsLanguage
    {
        get => _settingsLanguage;
        set => SetProperty(ref _settingsLanguage, value);
    }

    public bool IsContactsVisible
    {
        get => _isContactsVisible;
        set
        {
            if (!SetProperty(ref _isContactsVisible, value))
                return;
            OnPropertyChanged(nameof(SettingsEmailText));
            OnPropertyChanged(nameof(SettingsPhoneText));
            OnPropertyChanged(nameof(ToggleContactsText));
        }
    }

    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set
        {
            if (!SetProperty(ref _isShuffleEnabled, value))
                return;
            if (value)
                EnsureShuffleOrder();

            OnPropertyChanged(nameof(ShuffleLabel));
            OnPropertyChanged(nameof(IsRepeatOrShuffleActive));
            UpdateNowPlayingPreview();
        }
    }

    public PlaybackMode PlaybackMode
    {
        get => _playbackMode;
        set
        {
            if (!SetProperty(ref _playbackMode, value))
                return;

            OnPropertyChanged(nameof(RepeatLabel));
            OnPropertyChanged(nameof(IsRepeatEnabled));
            OnPropertyChanged(nameof(RepeatGlyph));
            OnPropertyChanged(nameof(IsRepeatOrShuffleActive));
            UpdateNowPlayingPreview();
        }
    }

    public bool IsQueuePanelOpen
    {
        get => _isQueuePanelOpen;
        set => SetProperty(ref _isQueuePanelOpen, value);
    }

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

    public string RepeatLabel => PlaybackMode switch
    {
        PlaybackMode.RepeatAll => "Repeat All", PlaybackMode.RepeatOne => "Repeat One", _ => "Repeat"
    };

    public bool IsRepeatEnabled => PlaybackMode != PlaybackMode.Normal;
    public bool IsRepeatOrShuffleActive => IsShuffleEnabled || IsRepeatEnabled;
    public string RepeatGlyph => PlaybackMode == PlaybackMode.RepeatOne ? "🔂" : "🔁";

    public string ActiveSection
    {
        get => _activeSection;
        set
        {
            if (SetProperty(ref _activeSection, value)) NotifySections();
        }
    }

    public bool IsTracksSection => ActiveSection == "tracks";
    public bool IsSearchSection => ActiveSection == "search";
    public bool IsPremiumSection => ActiveSection == "premium";
    public bool IsLikedSection => ActiveSection == "liked";
    public bool IsQueueSection => ActiveSection == "queue";
    public bool IsPlaylistsSection => ActiveSection == "playlists";
    public bool IsArtistSection => ActiveSection == "artist";
    public bool IsAlbumSection => ActiveSection == "album";
    public bool IsProfileSection => ActiveSection == "profile";
    public bool IsSettingsSection => ActiveSection == "settings";
    public bool IsSearchAllType => SelectedSearchType == "Все";
    public bool IsSearchArtistsType => SelectedSearchType == "Исполнители";
    public bool IsSearchTracksType => SelectedSearchType == "Треки";
    public bool IsSearchAlbumsType => SelectedSearchType == "Альбомы";
    public bool IsSearchPlaylistsType => SelectedSearchType == "Плейлисты";

    public string ArtistHeader
    {
        get => _artistHeader;
        set => SetProperty(ref _artistHeader, value);
    }

    public string AlbumHeader
    {
        get => _albumHeader;
        set => SetProperty(ref _albumHeader, value);
    }

    public string AlbumTitleText
    {
        get => _albumTitleText;
        private set => SetProperty(ref _albumTitleText, value);
    }

    public string AlbumArtistNameText
    {
        get => _albumArtistNameText;
        private set => SetProperty(ref _albumArtistNameText, value);
    }

    public string AlbumMetaText
    {
        get => _albumMetaText;
        private set => SetProperty(ref _albumMetaText, value);
    }

    public string PlaylistTitleText
    {
        get => _playlistTitleText;
        private set => SetProperty(ref _playlistTitleText, value);
    }

    public string PlaylistMetaText
    {
        get => _playlistMetaText;
        private set => SetProperty(ref _playlistMetaText, value);
    }

    public int AlbumTotalPlays => AlbumTracks.Sum(t => Math.Max(0, t.PlayCount));
    public string AlbumTotalPlaysText => $"{AlbumTotalPlays:N0} прослушиваний";

    public string ArtistHeroCoverPath
    {
        get => _artistHeroCoverPath;
        set => SetProperty(ref _artistHeroCoverPath, value);
    }

    public string ArtistAvatarPath
    {
        get => _artistAvatarPath;
        private set
        {
            if (!SetProperty(ref _artistAvatarPath, value))
                return;
            OnPropertyChanged(nameof(ArtistAvatarImage));
        }
    }

    public Bitmap? ArtistAvatarBitmap
    {
        get => _artistAvatarBitmap;
        private set
        {
            if (ReferenceEquals(_artistAvatarBitmap, value))
                return;

            _artistAvatarBitmap?.Dispose();
            _artistAvatarBitmap = value;
            OnPropertyChanged(nameof(ArtistAvatarBitmap));
            OnPropertyChanged(nameof(ArtistAvatarImage));
        }
    }

    public object? ArtistAvatarImage => (object?)ArtistAvatarBitmap ??
                                        (string.IsNullOrWhiteSpace(ArtistAvatarPath) ? null : ArtistAvatarPath);

    public string AlbumCoverPath
    {
        get => _albumCoverPath;
        set => SetProperty(ref _albumCoverPath, value);
    }

    public object? AlbumCoverImage => (object?)_albumCoverBitmap ??
                                      (string.IsNullOrWhiteSpace(AlbumCoverPath) ? null : AlbumCoverPath);

    public string PlaylistCoverPath
    {
        get => _playlistCoverPath;
        private set => SetProperty(ref _playlistCoverPath, value);
    }

    public object? PlaylistCoverImage => (object?)_playlistCoverBitmap ??
                                         (string.IsNullOrWhiteSpace(PlaylistCoverPath) ? null : PlaylistCoverPath);

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

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (SetProperty(ref _positionSeconds, value) && !_isSeeking)
                _audioPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0, value)));
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        set => SetProperty(ref _durationSeconds, value);
    }

    public string PositionText => TimeSpan.FromSeconds(Math.Max(PositionSeconds, 0)).ToString(@"mm\:ss");
    public string DurationText => TimeSpan.FromSeconds(Math.Max(DurationSeconds, 0)).ToString(@"mm\:ss");
    public string SeekPreviewText => TimeSpan.FromSeconds(Math.Max(_seekPreviewSeconds, 0)).ToString(@"mm\:ss");
    public bool IsSeekPreviewVisible => _isSeekPreviewVisible;
    public string CurrentTrackTitle => CurrentTrack?.Title ?? "Трек не выбран";
    public string CurrentTrackArtist => CurrentTrack?.Artist ?? string.Empty;
    public object? CurrentTrackCoverImage => CurrentTrack?.CoverImage;
    public TrackListItemDto? NextTrackPreview => GetUpcomingTrackPreview();
    public string NextTrackTitle => NextTrackPreview?.Title ?? CurrentTrackTitle;
    public string NextTrackArtist => NextTrackPreview?.Artist ?? CurrentTrackArtist;
    public object? NextTrackCoverImage => NextTrackPreview?.CoverImage ?? CurrentTrackCoverImage;
    public IReadOnlyList<QueueItemDto> UpcomingQueueItems => BuildUpcomingQueueItems();
    public bool IsPlaybackActive => _audioPlayer.IsPlaying;
    public bool IsPlaybackInactive => !_audioPlayer.IsPlaying;

    public string CurrentArtistMonthlyListenersText =>
        $"{Math.Max(0, (SelectedArtistTrack ?? CurrentTrack ?? SelectedTrack)?.PlayCount ?? _artistMonthlyStreams):N0} прослушиваний";

    public string ArtistMonthlyStreamsText => $"{Math.Max(0, _artistMonthlyStreams):N0} прослушиваний";
    public string CurrentArtistTotalStreamsText => $"{Math.Max(0, _currentArtistPlaysTotal):N0} прослушиваний";
    public string ArtistFollowersText => $"{Math.Max(0, _artistFollowersCount):N0} подписчиков";
    public string ArtistFollowButtonText => _isFollowingArtist ? "Отписаться" : "Подписаться";

    public bool IsArtistReleaseAllFilter =>
        string.Equals(_artistReleaseFilter, "all", StringComparison.OrdinalIgnoreCase);

    public bool IsArtistReleaseAlbumFilter =>
        string.Equals(_artistReleaseFilter, "album", StringComparison.OrdinalIgnoreCase);

    public bool IsArtistReleaseSingleFilter =>
        string.Equals(_artistReleaseFilter, "single", StringComparison.OrdinalIgnoreCase);

    public bool IsArtistReleasesModalOpen
    {
        get => _isArtistReleasesModalOpen;
        set => SetProperty(ref _isArtistReleasesModalOpen, value);
    }

    public bool CanShowAllArtistReleases => FilteredArtistReleases.Count > 5;

    public IReadOnlyList<ArtistReleaseItemDto> FilteredArtistReleases
    {
        get
        {
            IEnumerable<ArtistReleaseItemDto> query = ArtistReleases;
            if (IsArtistReleaseAlbumFilter)
                query = query.Where(x => x.IsAlbum);
            else if (IsArtistReleaseSingleFilter)
                query = query.Where(x => !x.IsAlbum);

            return query.ToList();
        }
    }

    public IReadOnlyList<ArtistReleaseItemDto> VisibleArtistReleases => FilteredArtistReleases.Take(5).ToList();
    public int PublicPlaylistsCount => Playlists.Count(p => p.IsPublic);
    public int LikedTracksCount => LikedTracks.Count;
    public string LikedOwnerName => string.IsNullOrWhiteSpace(ArtistName) ? DisplayName : ArtistName!;
    public string LikedHeaderStats => $"{LikedOwnerName} • {LikedTracksCount} треков";
    public string CurrentSubscriptionTitle => CurrentSubscription?.PlanTitle ?? "Free";

    public string CurrentSubscriptionAccent => ContainsToken(CurrentSubscriptionTitle, "student", "студ")
        ? "#D3C0F5"
        : ContainsToken(CurrentSubscriptionTitle, "premium", "прем")
            ? "#F7C45E"
            : "#F4C8D5";

    public string ProfileStatsText => $"{PublicPlaylistsCount} открытых плейлистов • {FollowingArtistsCount} подписки";

    public string SettingsEmailText =>
        string.IsNullOrWhiteSpace(ProfileEmail) ? "-" : (IsContactsVisible ? ProfileEmail : "************");

    public string SettingsPhoneText => string.IsNullOrWhiteSpace(ProfilePhone) ? "-" : (IsContactsVisible ? ProfilePhone : "************");
    public string ToggleContactsText => IsContactsVisible ? "Скрыть" : "Показать";
    public bool ShowFreePlanBillboard => IsFreePlanSelected;
    public bool IsFreePlanSelected => FreePlan is not null && CurrentSubscription?.PlanId == FreePlan.Id;
    public bool IsStudentPlanSelected => StudentPlan is not null && CurrentSubscription?.PlanId == StudentPlan.Id;
    public bool IsPremiumPlanSelected => PremiumPlan is not null && CurrentSubscription?.PlanId == PremiumPlan.Id;
    public string FreePlanButtonText => IsFreePlanSelected ? "Выбрано" : "Выбрать";
    public string StudentPlanButtonText => IsStudentPlanSelected ? "Выбрано" : "Выбрать";
    public string PremiumPlanButtonText => IsPremiumPlanSelected ? "Выбрано" : "Выбрать";
    public string PlaylistModalHeader => IsPlaylistEditMode ? "Редактировать плейлист" : "Создать плейлист";
    public string PlaylistSubmitText => IsPlaylistEditMode ? "Сохранить" : "Создать";
    public string FoundTracksText => $"Найдено: {Tracks.Count}";

    public string LikeButtonText =>
        SelectedTrack is not null && _likedSongIds.Contains(SelectedTrack.Id) ? "Убрать лайк" : "Лайк";
    public string AddCurrentTrackToLikedButtonText =>
        CurrentTrack is not null && _likedSongIds.Contains(CurrentTrack.Id)
            ? "Убрать из любимых"
            : "Добавить в любимые";

    public bool CanMoveQueueUp =>
        SelectedQueueItem is not null && QueueItems.Count > 1 && SelectedQueueItem.Position > 1;

    public bool CanMoveQueueDown => SelectedQueueItem is not null && QueueItems.Count > 1 &&
                                    SelectedQueueItem.Position < QueueItems.Count;

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
    public AsyncRelayCommand OpenAlbumArtistCommand { get; }
    public AsyncRelayCommand ToggleArtistFollowCommand { get; }
    public RelayCommand SetArtistReleaseAllCommand { get; }
    public RelayCommand SetArtistReleaseAlbumCommand { get; }
    public RelayCommand SetArtistReleaseSingleCommand { get; }
    public RelayCommand ShowAllArtistReleasesCommand { get; }
    public RelayCommand CloseArtistReleasesModalCommand { get; }
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
    public RelayCommand SetSectionProfileCommand { get; }
    public RelayCommand SetSectionSettingsCommand { get; }
    public RelayCommand ToggleQueuePanelCommand { get; }
    public RelayCommand SetSectionPlaylistsCommand { get; }
    public RelayCommand OpenCreatePlaylistModalCommand { get; }
    public RelayCommand OpenEditPlaylistModalCommand { get; }
    public RelayCommand ClosePlaylistModalCommand { get; }
    public RelayCommand SetSectionArtistCommand { get; }
    public RelayCommand SetSectionAlbumCommand { get; }
    public RelayCommand OpenEditProfileModalCommand { get; }
    public RelayCommand CloseEditProfileModalCommand { get; }
    public RelayCommand OpenEditContactsModalCommand { get; }
    public RelayCommand CloseEditContactsModalCommand { get; }
    public RelayCommand CloseAddTrackModalCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand NextTrackCommand { get; }
    public RelayCommand PreviousTrackCommand { get; }
    public RelayCommand MuteCommand { get; }
    public RelayCommand ToggleShuffleCommand { get; }
    public RelayCommand ToggleRepeatModeCommand { get; }
    public RelayCommand ToggleContactsVisibilityCommand { get; }
    public RelayCommand AddTrackCommand { get; }
    public AsyncRelayCommand AddCurrentTrackToLikedCommand { get; }
    public AsyncRelayCommand AddCurrentTrackToPlaylistCommand { get; }
    public AsyncRelayCommand SubmitAddTrackCommand { get; }
    public AsyncRelayCommand SaveContactsCommand { get; }
    public AsyncRelayCommand ChangePasswordCommand { get; }
    public AsyncRelayCommand SaveArtistNameCommand { get; }
    public AsyncRelayCommand SaveProfileChangesCommand { get; }
    public AsyncRelayCommand SelectFreePlanCommand { get; }
    public AsyncRelayCommand SelectStudentPlanCommand { get; }
    public AsyncRelayCommand SelectPremiumPlanCommand { get; }
}
