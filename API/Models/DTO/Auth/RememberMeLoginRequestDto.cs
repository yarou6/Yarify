using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class RememberMeLoginRequestDto
{
    [Required]
    public string Login { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

