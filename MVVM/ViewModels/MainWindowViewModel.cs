
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
using NAudio.Wave;

namespace MVVM.ViewModels;

public class MainWindowViewModel : BaseVM
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
    private bool _isEmailVisible;

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
        ArtistReleases = new ObservableCollection<ArtistReleaseItemDto>();
        AlbumTracks = new ObservableCollection<TrackListItemDto>();
        RecentTracks = new ObservableCollection<TrackListItemDto>();
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

        CreatePlaylistCommand = new AsyncRelayCommand(CreatePlaylistAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        SavePlaylistModalCommand = new AsyncRelayCommand(SavePlaylistModalAsync, () => !string.IsNullOrWhiteSpace(NewPlaylistTitle));
        OpenCreatePlaylistModalCommand = new RelayCommand(OpenCreatePlaylistModal);
        OpenEditPlaylistModalCommand = new RelayCommand(OpenEditPlaylistModal, () => SelectedPlaylist is not null);
        ClosePlaylistModalCommand = new RelayCommand(() => IsPlaylistModalOpen = false);
        DeletePlaylistCommand = new AsyncRelayCommand(DeleteSelectedPlaylistAsync, () => SelectedPlaylist is not null);
        AddSelectedTrackToPlaylistCommand = new AsyncRelayCommand(AddSelectedTrackToPlaylistAsync, () => SelectedTrack is not null && SelectedPlaylist is not null);
        RemovePlaylistTrackCommand = new AsyncRelayCommand(RemoveSelectedPlaylistTrackAsync, () => SelectedPlaylistTrack is not null && SelectedPlaylist is not null);

        OpenSelectedArtistCommand = new AsyncRelayCommand(OpenSelectedArtistAsync, () => (SelectedTrack ?? CurrentTrack) is not null);
        OpenAlbumArtistCommand = new AsyncRelayCommand(OpenAlbumArtistAsync, () => (SelectedAlbumTrack?.ArtistUserId ?? _currentArtistUserId) > 0);
        ToggleArtistFollowCommand = new AsyncRelayCommand(ToggleArtistFollowAsync, () => _currentArtistUserId > 0);
        SetArtistReleaseAllCommand = new RelayCommand(() => SetArtistReleaseFilter("all"));
        SetArtistReleaseAlbumCommand = new RelayCommand(() => SetArtistReleaseFilter("album"));
        SetArtistReleaseSingleCommand = new RelayCommand(() => SetArtistReleaseFilter("single"));
        ShowAllArtistReleasesCommand = new RelayCommand(OpenArtistReleasesModal);
        CloseArtistReleasesModalCommand = new RelayCommand(() => IsArtistReleasesModalOpen = false);
        OpenSelectedAlbumCommand = new AsyncRelayCommand(OpenSelectedAlbumAsync, () => (SelectedTrack?.AlbumId ?? CurrentTrack?.AlbumId) is not null);
        OpenArtistAlbumCommand = new AsyncRelayCommand(OpenSelectedArtistAlbumAsync, () => SelectedArtistAlbum is not null);

        PlaySelectedTrackCommand = new AsyncRelayCommand(async () => await PlayFromTracksAsync(SelectedTrack), () => SelectedTrack is not null);
        PlayLikedTrackCommand = new AsyncRelayCommand(async () => await PlayFromLikedAsync(SelectedLikedTrack), () => SelectedLikedTrack is not null);
        PlayQueueTrackCommand = new AsyncRelayCommand(async () => await PlayFromQueueAsync(SelectedQueueItem?.Track), () => SelectedQueueItem is not null);
        PlayPlaylistTrackCommand = new AsyncRelayCommand(async () => await PlayFromPlaylistAsync(SelectedPlaylistTrack), () => SelectedPlaylistTrack is not null);
        PlayArtistTrackCommand = new AsyncRelayCommand(async () => await PlayFromArtistAsync(SelectedArtistTrack), () => SelectedArtistTrack is not null);
        PlayAlbumTrackCommand = new AsyncRelayCommand(PlayAlbumPrimaryAsync, () => SelectedAlbumTrack is not null || AlbumTracks.Count > 0);

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
        AddCurrentTrackToLikedCommand = new AsyncRelayCommand(AddCurrentTrackToLikedAsync, () => CurrentTrack is not null);
        AddCurrentTrackToPlaylistCommand = new AsyncRelayCommand(AddCurrentTrackToPlaylistAsync, () => CurrentTrack is not null && SelectedPlaylist is not null);
        AddTrackCommand = new RelayCommand(AddTrackAction);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);
        SaveArtistNameCommand = new AsyncRelayCommand(SaveArtistNameAsync, CanSaveArtistName);
        SaveProfileChangesCommand = new AsyncRelayCommand(SaveProfileChangesAsync, CanSaveProfileChanges);
        SelectFreePlanCommand = new AsyncRelayCommand(() => SelectPlanAsync(FreePlan), () => CanSelectPlan(FreePlan));
        SelectStudentPlanCommand = new AsyncRelayCommand(() => SelectPlanAsync(StudentPlan), () => CanSelectPlan(StudentPlan));
        SelectPremiumPlanCommand = new AsyncRelayCommand(() => SelectPlanAsync(PremiumPlan), () => CanSelectPlan(PremiumPlan));

        PlayPauseCommand = new RelayCommand(PlayPause, () => CurrentTrack is not null);
        NextTrackCommand = new RelayCommand(() => _ = PlayNextTrackAsync());
        PreviousTrackCommand = new RelayCommand(PlayPreviousTrack);
        MuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        ToggleShuffleCommand = new RelayCommand(() => IsShuffleEnabled = !IsShuffleEnabled);
        ToggleRepeatModeCommand = new RelayCommand(ToggleRepeatMode);
        ToggleEmailVisibilityCommand = new RelayCommand(() => IsEmailVisible = !IsEmailVisible);

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
    public ObservableCollection<string> GenreOptions { get; }
    public ObservableCollection<SubscriptionPlanDto> SubscriptionPlans { get; }
    public ObservableCollection<TrackListItemDto> SearchResultTracks { get; }
    public ObservableCollection<ArtistSearchItemDto> SearchResultArtists { get; }
    public ObservableCollection<AlbumListItemDto> SearchResultAlbums { get; }
    public ObservableCollection<PlaylistListItemDto> SearchResultPlaylists { get; }
    public ObservableCollection<AddTrackGenreItemViewModel> AddTrackGenres { get; }
    public ObservableCollection<AddTrackGenreItemViewModel> FilteredAddTrackGenres { get; }
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
    public SubscriptionPlanDto? FreePlan
    {
        get => _freePlan;
        private set
        {
            if (!SetProperty(ref _freePlan, value))
                return;
            OnPropertyChanged(nameof(IsFreePlanSelected));
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
    public string ProfileLogin { get => _profileLogin; private set => SetProperty(ref _profileLogin, value); }
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
    public string UserInitial => string.IsNullOrWhiteSpace(DisplayName) ? "Y" : DisplayName.Trim()[0].ToString().ToUpperInvariant();
    public bool IsArtistOrAdmin => RoleTitle.Equals("Artist", StringComparison.OrdinalIgnoreCase) || RoleTitle.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool HasArtistName => !string.IsNullOrWhiteSpace(ArtistName);
    public string ProfileArtistNameText => HasArtistName ? ArtistName! : "Имя артиста не задано";

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
    public bool AllowExplicitContent
    {
        get => _allowExplicitContent;
        set
        {
            if (!SetProperty(ref _allowExplicitContent, value))
                return;
            _ = SaveSettingsAsync();
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
            OnPropertyChanged(nameof(IsStudentPlanSelected));
            OnPropertyChanged(nameof(IsPremiumPlanSelected));
            OnPropertyChanged(nameof(FreePlanButtonText));
            OnPropertyChanged(nameof(StudentPlanButtonText));
            OnPropertyChanged(nameof(PremiumPlanButtonText));
            RaiseCanExecutes();
        }
    }
    public string SettingsArtistNameInput { get => _settingsArtistNameInput; set => SetProperty(ref _settingsArtistNameInput, value, RaiseCanExecutes); }
    public string EditDisplayName { get => _editDisplayName; set => SetProperty(ref _editDisplayName, value, RaiseCanExecutes); }
    public string EditAvatarPath { get => _editAvatarPath; set => SetProperty(ref _editAvatarPath, value, RaiseCanExecutes); }
    public bool IsEditProfileModalOpen { get => _isEditProfileModalOpen; set => SetProperty(ref _isEditProfileModalOpen, value); }
    public bool IsEditContactsModalOpen { get => _isEditContactsModalOpen; set => SetProperty(ref _isEditContactsModalOpen, value); }
    public string EditEmailInput { get => _editEmailInput; set => SetProperty(ref _editEmailInput, value, RaiseCanExecutes); }
    public string EditPhoneInput { get => _editPhoneInput; set => SetProperty(ref _editPhoneInput, value, RaiseCanExecutes); }
    public bool IsAddTrackModalOpen { get => _isAddTrackModalOpen; set => SetProperty(ref _isAddTrackModalOpen, value); }
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
    public string AddTrackAlbumTitle { get => _addTrackAlbumTitle; set => SetProperty(ref _addTrackAlbumTitle, value, RaiseCanExecutes); }
    public string AddTrackPlannedCountInput { get => _addTrackPlannedCountInput; set => SetProperty(ref _addTrackPlannedCountInput, value, RaiseCanExecutes); }
    public string AddTrackTitleInput { get => _addTrackTitleInput; set => SetProperty(ref _addTrackTitleInput, value, RaiseCanExecutes); }
    public string AddTrackDurationInput { get => _addTrackDurationInput; set => SetProperty(ref _addTrackDurationInput, value, RaiseCanExecutes); }
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
    public string AddTrackStreamUrl { get => _addTrackStreamUrl; set => SetProperty(ref _addTrackStreamUrl, value); }
    public string AddTrackAlbumCoverPath { get => _addTrackAlbumCoverPath; set => SetProperty(ref _addTrackAlbumCoverPath, value); }
    public string AddTrackCoverPath { get => _addTrackCoverPath; set => SetProperty(ref _addTrackCoverPath, value); }
    public bool AddTrackExplicit { get => _addTrackExplicit; set => SetProperty(ref _addTrackExplicit, value); }
    public string AddTrackProgressText => IsAlbumTrackMode
        ? (_albumTracksRemaining > 0
            ? $"Осталось добавить треков: {_albumTracksRemaining}"
            : "Режим альбома: укажи название и количество треков.")
        : "Режим сингла: можно добавлять треки по одному.";
    public string CurrentPasswordInput { get => _currentPasswordInput; set => SetProperty(ref _currentPasswordInput, value, RaiseCanExecutes); }
    public string NewPasswordInput { get => _newPasswordInput; set => SetProperty(ref _newPasswordInput, value, RaiseCanExecutes); }
    public string ConfirmPasswordInput { get => _confirmPasswordInput; set => SetProperty(ref _confirmPasswordInput, value, RaiseCanExecutes); }
    public string SettingsLanguage { get => _settingsLanguage; set => SetProperty(ref _settingsLanguage, value); }
    public bool IsEmailVisible
    {
        get => _isEmailVisible;
        set
        {
            if (!SetProperty(ref _isEmailVisible, value))
                return;
            OnPropertyChanged(nameof(SettingsEmailText));
            OnPropertyChanged(nameof(ToggleEmailText));
        }
    }

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
    public bool IsProfileSection => ActiveSection == "profile";
    public bool IsSettingsSection => ActiveSection == "settings";
    public bool IsSearchAllType => SelectedSearchType == "Все";
    public bool IsSearchArtistsType => SelectedSearchType == "Исполнители";
    public bool IsSearchTracksType => SelectedSearchType == "Треки";
    public bool IsSearchAlbumsType => SelectedSearchType == "Альбомы";
    public bool IsSearchPlaylistsType => SelectedSearchType == "Плейлисты";

    public string ArtistHeader { get => _artistHeader; set => SetProperty(ref _artistHeader, value); }
    public string AlbumHeader { get => _albumHeader; set => SetProperty(ref _albumHeader, value); }
    public string AlbumTitleText { get => _albumTitleText; private set => SetProperty(ref _albumTitleText, value); }
    public string AlbumArtistNameText { get => _albumArtistNameText; private set => SetProperty(ref _albumArtistNameText, value); }
    public string AlbumMetaText { get => _albumMetaText; private set => SetProperty(ref _albumMetaText, value); }
    public int AlbumTotalPlays => AlbumTracks.Sum(t => Math.Max(0, t.PlayCount));
    public string AlbumTotalPlaysText => $"{AlbumTotalPlays:N0} прослушиваний";
    public string ArtistHeroCoverPath { get => _artistHeroCoverPath; set => SetProperty(ref _artistHeroCoverPath, value); }
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
    public object? ArtistAvatarImage => (object?)ArtistAvatarBitmap ?? (string.IsNullOrWhiteSpace(ArtistAvatarPath) ? null : ArtistAvatarPath);
    public string AlbumCoverPath { get => _albumCoverPath; set => SetProperty(ref _albumCoverPath, value); }
    public object? AlbumCoverImage => (object?)_albumCoverBitmap ?? (string.IsNullOrWhiteSpace(AlbumCoverPath) ? null : AlbumCoverPath);

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
    public object? CurrentTrackCoverImage => CurrentTrack?.CoverImage;
    public bool IsPlaybackActive => _audioPlayer.IsPlaying;
    public bool IsPlaybackInactive => !_audioPlayer.IsPlaying;
    public string CurrentArtistMonthlyListenersText => $"{Math.Max(0, (SelectedArtistTrack ?? CurrentTrack ?? SelectedTrack)?.PlayCount ?? _artistMonthlyStreams):N0} прослушиваний";
    public string ArtistMonthlyStreamsText => $"{Math.Max(0, _artistMonthlyStreams):N0} прослушиваний";
    public string ArtistFollowersText => $"{Math.Max(0, _artistFollowersCount):N0} подписчиков";
    public string ArtistFollowButtonText => _isFollowingArtist ? "Отписаться" : "Подписаться";
    public bool IsArtistReleaseAllFilter => string.Equals(_artistReleaseFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsArtistReleaseAlbumFilter => string.Equals(_artistReleaseFilter, "album", StringComparison.OrdinalIgnoreCase);
    public bool IsArtistReleaseSingleFilter => string.Equals(_artistReleaseFilter, "single", StringComparison.OrdinalIgnoreCase);
    public bool IsArtistReleasesModalOpen { get => _isArtistReleasesModalOpen; set => SetProperty(ref _isArtistReleasesModalOpen, value); }
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
    public string SettingsEmailText => string.IsNullOrWhiteSpace(ProfileEmail) ? "-" : (IsEmailVisible ? ProfileEmail : "***");
    public string SettingsPhoneText => string.IsNullOrWhiteSpace(ProfilePhone) ? "-" : ProfilePhone;
    public string ToggleEmailText => IsEmailVisible ? "Скрыть" : "Показать";
    public bool IsFreePlanSelected => FreePlan is not null && CurrentSubscription?.PlanId == FreePlan.Id;
    public bool IsStudentPlanSelected => StudentPlan is not null && CurrentSubscription?.PlanId == StudentPlan.Id;
    public bool IsPremiumPlanSelected => PremiumPlan is not null && CurrentSubscription?.PlanId == PremiumPlan.Id;
    public string FreePlanButtonText => IsFreePlanSelected ? "Выбрано" : "Выбрать";
    public string StudentPlanButtonText => IsStudentPlanSelected ? "Выбрано" : "Выбрать";
    public string PremiumPlanButtonText => IsPremiumPlanSelected ? "Выбрано" : "Выбрать";
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
    public RelayCommand ToggleEmailVisibilityCommand { get; }
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
        if (!GenreOptions.Contains(SelectedGenre))
            SelectedGenre = "Все";
    }

    private async Task LoadTracksAsync()
    {
        IsBusy = true;
        try
        {
            var (items, error) = await _authSessionService.ApiClient.GetTracksAsync(SearchText, SelectedGenre, "title");
            await HydrateAlbumTitlesAsync(items);
            Tracks.Clear();
            foreach (var item in items) Tracks.Add(item);

            Status = string.IsNullOrWhiteSpace(error)
                ? "Треки обновлены."
                : $"Ошибка треков: {error}";

            await BuildSearchResultsAsync();
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

    public async Task PlayTrackFromUiAsync(TrackListItemDto track)
    {
        SelectedTrack = track;
        if (AlbumTracks.Contains(track))
            SelectedAlbumTrack = track;
        await PlayTrackAsync(track);
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
        _playbackContextKey = "queue";
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
        var (eventId, error) = await _authSessionService.ApiClient.StartListeningEventAsync(track.Id, "Direct", null, DateTime.UtcNow);
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
        if (string.IsNullOrWhiteSpace(track.Source)) { Status = "У трека нет Source."; return; }
        EnsurePlaybackContextForTrack(track);

        try
        {
            await CompleteActiveListeningEventAsync(forceCompleted: false);
            _audioPlayer.Load(track.Source);
            _audioPlayer.Volume = IsMuted ? 0d : VolumePercent / 100d;
            _audioPlayer.Play();
            CurrentTrack = track;
            SelectedTrack = track;
            var albumTrack = AlbumTracks.FirstOrDefault(t => t.Id == track.Id);
            if (albumTrack is not null)
            {
                SelectedAlbumTrack = albumTrack;
                _playbackContextKey = "album";
            }
            RememberTrack(track);
            Status = $"Сейчас играет: {track.Title}";
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
            return;
        }

        await PlayTrackAsync(next);
    }

    private TrackListItemDto? NextShuffled(IReadOnlyList<TrackListItemDto> activeList)
    {
        if (activeList.Count == 0)
            return null;

        if (CurrentTrack is null)
            return activeList[_random.Next(activeList.Count)];

        var candidates = activeList.Where(t => t.Id != CurrentTrack.Id).ToList();
        if (candidates.Count == 0)
            return PlaybackMode == PlaybackMode.RepeatAll ? activeList[0] : null;

        return candidates[_random.Next(candidates.Count)];
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
    }

    private void ToggleRepeatMode() => PlaybackMode = PlaybackMode switch { PlaybackMode.Normal => PlaybackMode.RepeatAll, PlaybackMode.RepeatAll => PlaybackMode.RepeatOne, _ => PlaybackMode.Normal };

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

            using var reader = new AudioFileReader(path);
            var seconds = Math.Max(1, (int)Math.Round(reader.TotalTime.TotalSeconds));
            AddTrackDurationInput = seconds.ToString();
        }
        catch
        {
            // ignored: unsupported codec/path, user can set duration manually.
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
        AllowExplicitContent = AllowExplicitContent
    });

    private void UpdatePlayback()
    {
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentTrackArtist));
        OnPropertyChanged(nameof(CurrentTrackCoverImage));
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
        OnPropertyChanged(nameof(IsArtistReleaseAllFilter));
        OnPropertyChanged(nameof(IsArtistReleaseAlbumFilter));
        OnPropertyChanged(nameof(IsArtistReleaseSingleFilter));
        OnPropertyChanged(nameof(FilteredArtistReleases));
        OnPropertyChanged(nameof(VisibleArtistReleases));
        OnPropertyChanged(nameof(CanShowAllArtistReleases));
    }

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
        RaiseCanExecutes();
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
        OnPropertyChanged(nameof(IsProfileSection));
        OnPropertyChanged(nameof(IsSettingsSection));
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
        Status = _isFollowingArtist ? "Подписка оформлена." : "Подписка отменена.";
        RaiseCanExecutes();
    }

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

    private void OpenArtistReleasesModal()
    {
        if (FilteredArtistReleases.Count == 0)
            return;
        IsArtistReleasesModalOpen = true;
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

    public void SetAvatarPreviewFromLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        UserAvatarSource = new Uri(path).AbsoluteUri;
        ApplyAvatarBitmapFromResolvedSource();
    }

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












