using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using MVVM.Models.Playback;
using MVVM.ViewModels;

namespace MVVM.Views;

public partial class MainWindow : Window
{
    private ContextMenu? _activeTrackContextMenu;
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void TrackCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackListItemDto track } control)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        var point = e.GetCurrentPoint(control);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            ShowTrackContextMenu(control, track, vm);
            e.Handled = true;
            return;
        }

        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        await vm.PlayTrackFromUiAsync(track);
    }

    private void ShowTrackContextMenu(Control anchor, TrackListItemDto track, MainWindowViewModel vm)
    {
        _activeTrackContextMenu?.Close();

        var addNext = new MenuItem { Header = "▶ Добавить в очередь (следующим)" };
        addNext.Click += async (_, _) => await vm.AddTrackToQueueNextAsync(track);
        var menuItems = new List<object> { addNext };
        if (vm.IsPlaylistsSection && vm.IsTrackInSelectedPlaylist(track.Id) && vm.SelectedPlaylist is not null)
        {
            var removeFromPlaylist = new MenuItem { Header = $"✕ Убрать из плейлиста: {vm.SelectedPlaylist.Title}" };
            removeFromPlaylist.Click += async (_, _) => await vm.RemoveTrackFromSelectedPlaylistByIdAsync(track.Id);
            menuItems.Add(removeFromPlaylist);
        }

        if (vm.IsLikedSection && vm.IsTrackLiked(track.Id))
        {
            var removeFromLiked = new MenuItem { Header = "✕ Убрать из любимых" };
            removeFromLiked.Click += async (_, _) => await vm.RemoveTrackFromLikedByIdAsync(track.Id);
            menuItems.Add(removeFromLiked);
        }

        _activeTrackContextMenu = new ContextMenu
        {
            ItemsSource = menuItems,
            PlacementTarget = anchor
        };

        _activeTrackContextMenu.Open(anchor);
    }

    private async void AlbumCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: AlbumListItemDto album })
            return;

        var point = e.GetCurrentPoint(sender as Control ?? this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenAlbumByIdFromUiAsync(album.Id);
    }

    private async void ReleaseCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ArtistReleaseItemDto release })
            return;

        var point = e.GetCurrentPoint(sender as Control ?? this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenArtistReleaseFromUiAsync(release);
    }

    private async void ArtistCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ArtistSearchItemDto artist })
            return;

        var point = e.GetCurrentPoint(sender as Control ?? this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenArtistByIdFromUiAsync(artist.ArtistUserId);
    }

    private async void SearchPlaylistCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: PlaylistListItemDto playlist } control)
            return;

        var point = e.GetCurrentPoint(control);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenPlaylistFromSearchAsync(playlist);
        
    }

    #pragma warning disable CS0618
    private void PlaylistDropTarget_OnDragOver(object? sender, DragEventArgs e)
    {
        if (TryGetSongId(e.Data, out _))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private async void PlaylistDropTarget_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is not Control { DataContext: PlaylistListItemDto playlist })
            return;

        if (!TryGetSongId(e.Data, out var songId))
            return;

        await vm.AddTrackToPlaylistByIdsAsync(songId, playlist.Id);
        e.Handled = true;
    }

    private void PlayerProgressSlider_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is not Slider slider)
            return;

        if (slider.Bounds.Width <= 0)
            return;

        var pointer = e.GetPosition(slider);
        var ratio = Math.Clamp(pointer.X / slider.Bounds.Width, 0, 1);
        var previewSeconds = Math.Max(0, vm.DurationSeconds) * ratio;
        vm.UpdateSeekPreview(previewSeconds);
    }

    private void PlayerProgressSlider_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.HideSeekPreview();
    }

    private static bool TryGetSongId(IDataObject data, out int songId)
    {
        songId = 0;

        var raw = data.Contains("yarify/song-id")
            ? data.Get("yarify/song-id")?.ToString()
            : data.GetText();

        return int.TryParse(raw, out songId);
    }
    #pragma warning restore CS0618

    private async void BrowseAvatar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var path = await PickSingleFilePathAsync("Выбор аватарки", ImageFileTypes());
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.EditAvatarPath = path;
            vm.SetAvatarPreviewFromLocalPath(path);
        }
    }

    private async void BrowseAlbumCover_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var path = await PickSingleFilePathAsync("Выбор обложки альбома", ImageFileTypes());
        if (!string.IsNullOrWhiteSpace(path))
            vm.AddTrackAlbumCoverPath = path;
    }

    private async void BrowseTrackCover_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var path = await PickSingleFilePathAsync("Выбор обложки трека", ImageFileTypes());
        if (!string.IsNullOrWhiteSpace(path))
            vm.AddTrackCoverPath = path;
    }

    private async void BrowsePlaylistCover_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var path = await PickSingleFilePathAsync("Выбор обложки плейлиста", ImageFileTypes());
        if (!string.IsNullOrWhiteSpace(path))
            vm.NewPlaylistCoverPath = path;
    }

    private async void BrowseAudioFile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var path = await PickSingleFilePathAsync("Выбор аудио файла", AudioFileTypes());
        if (!string.IsNullOrWhiteSpace(path))
            vm.AddTrackLocalPath = path;
    }

    private async void TrackArtist_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackListItemDto track })
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenArtistByIdFromUiAsync(track.ArtistUserId);
        e.Handled = true;
    }

    private async void CurrentArtist_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var artistId = vm.CurrentTrack?.ArtistUserId ?? vm.SelectedTrack?.ArtistUserId ?? 0;
        if (artistId <= 0)
            return;

        await vm.OpenArtistByIdFromUiAsync(artistId);
        e.Handled = true;
    }

    private async void TrackAlbumBadge_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackListItemDto track })
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenTrackAlbumFromUiAsync(track);
        e.Handled = true;
    }

    private async void HomeCollectionCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: HomeMediaCollectionItemDto collection } control)
            return;

        var point = e.GetCurrentPoint(control);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenHomeCollectionAsync(collection);
    }

    private async void AddCurrentTrackToPlaylistMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { Tag: int playlistId })
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.AddCurrentTrackToPlaylistAsync(playlistId);
    }

    private void PlaylistItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var point = e.GetCurrentPoint(sender as Control ?? this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        vm.ActiveSection = "playlists";
    }

    private async void OverviewGenreCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: OverviewGenreShelfDto shelf } control)
            return;

        var point = e.GetCurrentPoint(control);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenOverviewGenreAsync(shelf.Genre);
    }

    private async void FollowingArtist_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: MVVM.Models.Profile.FollowingArtistItemDto artist } control)
            return;

        var point = e.GetCurrentPoint(control);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.OpenArtistByIdFromUiAsync(artist.ArtistUserId);
    }

    private async Task<string?> PickSingleFilePathAsync(string title, IReadOnlyList<FilePickerFileType> filters)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider is null || !topLevel.StorageProvider.CanOpen)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return null;

        return file.TryGetLocalPath();
    }

    private static IReadOnlyList<FilePickerFileType> ImageFileTypes()
    {
        return new[]
        {
            new FilePickerFileType("Изображения")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
            }
        };
    }

    private static IReadOnlyList<FilePickerFileType> AudioFileTypes()
    {
        return new[]
        {
            new FilePickerFileType("Аудио")
            {
                Patterns = new[] { "*.mp3", "*.wav", "*.flac", "*.ogg", "*.m4a", "*.aac" }
            }
        };
    }
}
