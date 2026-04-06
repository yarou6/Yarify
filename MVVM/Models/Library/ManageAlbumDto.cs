namespace MVVM.Models.Library;

public sealed class ManageAlbumDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public int TracksCount { get; set; }
}
