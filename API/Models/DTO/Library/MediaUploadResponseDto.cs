namespace API.Models.DTO;

public sealed class MediaUploadResponseDto
{
    public string RelativePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
