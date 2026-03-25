using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class RegisterRequestDto
{
    [Required]
    public string Login { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    public string ConfirmPassword { get; set; } = null!;

    [Required]
    public string DisplayName { get; set; } = null!;
}

