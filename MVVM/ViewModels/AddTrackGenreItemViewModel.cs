using MVVM.Tools;

namespace MVVM.ViewModels;

public sealed class AddTrackGenreItemViewModel : BaseVM
{
    private bool _isSelected;

    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
