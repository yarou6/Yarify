namespace MVVM.Models.Library;

public sealed class CreateAlbumRequestDto
{
    public string Title { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public string? CoverPath { get; set; }
}
