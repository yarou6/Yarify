using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ForgotPasswordRequestDto
{
    [Required]
    public string LoginOrEmail { get; set; } = null!;
}

