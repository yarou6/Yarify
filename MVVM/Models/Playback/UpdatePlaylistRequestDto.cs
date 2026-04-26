namespace MVVM.Models.Playback;

public sealed class UpdatePlaylistRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public bool IsPublic { get; set; }
}
