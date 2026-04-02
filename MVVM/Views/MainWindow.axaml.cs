using Avalonia.Controls;
using Avalonia.Input;
using MVVM.Models.Playback;
using MVVM.ViewModels;

namespace MVVM.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void TrackCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackListItemDto track })
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var data = new DataObject();
        data.Set("yarify/song-id", track.Id.ToString());
        data.Set(DataFormats.Text, track.Id.ToString());
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

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
}
