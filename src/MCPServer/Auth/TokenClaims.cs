using System.Security.Claims;

namespace MCPServer.Auth;

internal static class TokenClaims
{
    public const string Subject = "sub";
    public const string ObjectId = "oid";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
    public const string Roles = "roles";

    // Microsoft Entra ID emits "scp"; most other authorization servers emit "scope".
    private static readonly string[] ScopeClaimTypes = ["scp", "scope"];

    public static IReadOnlyList<string> GetScopes(ClaimsPrincipal principal) =>
        [.. ScopeClaimTypes
            .SelectMany(principal.FindAll)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)];

    public static IReadOnlyList<string> GetRoles(ClaimsPrincipal principal) =>
        [.. principal.FindAll(Roles)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)];
}
