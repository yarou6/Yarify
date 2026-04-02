namespace MVVM.Models.Playback;

public sealed class CreatePlaylistRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
}
