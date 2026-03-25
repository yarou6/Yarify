using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class LoginRequestDto
{
    [Required]
    public string Login { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}


