namespace API.Services.Password;

public sealed class PasswordValidationService : IPasswordValidationService
{
    public string? ValidatePasswordPolicy(string password)
    {
        if (password.Length < 8)
            return "Пароль должен содержать минимум 8 символов.";

        if (!password.Any(char.IsUpper))
            return "Пароль должен содержать хотя бы одну заглавную букву.";

        if (!password.Any(char.IsLower))
            return "Пароль должен содержать хотя бы одну строчную букву.";

        if (!password.Any(char.IsDigit))
            return "Пароль должен содержать хотя бы одну цифру.";

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            return "Пароль должен содержать хотя бы один спецсимвол.";

        return null;
    }
}

