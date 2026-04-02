namespace API.Models.DTO;

public sealed class LyricItemDto
{
    public int SongId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LyricsText { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
