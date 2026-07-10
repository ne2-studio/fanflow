using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

/// <summary>
/// Resolves the logged-in user from the OIDC "sub" claim on the current request's validated JWT.
/// </summary>
public class HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor) : ICurrentUserProvider
{
    public string GetUserId()
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No HTTP context is available to resolve the current user.");

        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return userId ?? throw new InvalidOperationException("The current user token has no 'sub' claim.");
    }
}
