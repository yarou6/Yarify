namespace MVVM.Models.Profile;

public sealed class UpdateProfileRequestDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
