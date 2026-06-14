using System.Security.Claims;

namespace API.Services.UserContext;

public sealed class UserContextService : IUserContextService
{
    // Готовит и возвращает нужные данные.
    public int GetRequiredUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("В JWT отсутствует NameIdentifier.");

        return int.Parse(id);
    }
}

