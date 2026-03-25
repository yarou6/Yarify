namespace API.Models.DTO;

public sealed class ForgotPasswordResponseDto
{
    public string Message { get; set; } = null!;

    public string? ResetToken { get; set; }
}

