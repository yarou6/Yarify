using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

