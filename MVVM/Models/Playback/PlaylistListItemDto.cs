namespace MVVM.Models.Playback;

public sealed class PlaylistListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int TracksCount { get; set; }

    public string Subtitle => string.IsNullOrWhiteSpace(Description) ? $"Треков: {TracksCount}" : Description!;
}
