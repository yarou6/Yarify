namespace API.Services.Password;

public interface IPasswordValidationService
{
    string? ValidatePasswordPolicy(string password);
}

