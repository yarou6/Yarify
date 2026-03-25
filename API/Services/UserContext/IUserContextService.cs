using System.Security.Claims;

namespace API.Services.UserContext;

public interface IUserContextService
{
    int GetRequiredUserId(ClaimsPrincipal user);
}

