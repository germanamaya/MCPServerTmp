using System.Security.Claims;

namespace MCPServer.Auth;

public sealed record CurrentUser(
    string Id,
    string? Name,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Roles);

public interface ICurrentUserService
{
    /// <summary>Returns the signed-in caller, or <c>null</c> when the principal is not authenticated.</summary>
    CurrentUser? GetCurrentUser(ClaimsPrincipal principal);
}

internal sealed class ClaimsCurrentUserService : ICurrentUserService
{
    public CurrentUser? GetCurrentUser(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var id = principal.FindFirstValue(TokenClaims.Subject) ?? principal.FindFirstValue(TokenClaims.ObjectId);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var name = principal.FindFirstValue(TokenClaims.Name) ?? principal.FindFirstValue(TokenClaims.PreferredUsername);
        return new CurrentUser(id, name, TokenClaims.GetScopes(principal), TokenClaims.GetRoles(principal));
    }
}
