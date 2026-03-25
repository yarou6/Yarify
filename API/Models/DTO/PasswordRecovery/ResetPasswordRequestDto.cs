using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ResetPasswordRequestDto
{
    [Required]
    public string ResetToken { get; set; } = null!;

    [Required]
    public string NewPassword { get; set; } = null!;

    [Required]
    public string ConfirmNewPassword { get; set; } = null!;
}

