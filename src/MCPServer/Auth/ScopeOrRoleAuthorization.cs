using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MCPServer.Auth;

/// <summary>
/// Requires the token to carry one of the configured scopes or application roles.
/// A valid audience alone is not enough: an identity provider may issue tokens for this
/// audience to callers that were never granted access to it.
/// </summary>
internal sealed class ScopeOrRoleRequirement : IAuthorizationRequirement;

internal sealed class ScopeOrRoleAuthorizationHandler(IOptions<AuthSettings> settings)
    : AuthorizationHandler<ScopeOrRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeOrRoleRequirement requirement)
    {
        var auth = settings.Value;
        var acceptedScopes = auth.Scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).ToArray();
        var acceptedRoles = auth.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).ToArray();

        if (acceptedScopes.Length == 0 && acceptedRoles.Length == 0)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var tokenScopes = TokenClaims.GetScopes(context.User);
        var tokenRoles = TokenClaims.GetRoles(context.User);

        if (acceptedScopes.Any(scope => tokenScopes.Contains(scope) || tokenScopes.Contains(GetShortScopeName(scope)))
            || acceptedRoles.Any(tokenRoles.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    // Entra ID clients request "api://{app-id}/{scope}" but the token's scp claim holds only "{scope}".
    private static string GetShortScopeName(string scope) => scope[(scope.LastIndexOf('/') + 1)..];
}
