using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MCPServer.Tests.Support;

/// <summary>
/// Stands in for the real authority in tests: it signs tokens with its own key, and the test
/// server trusts that key through the regular token validation pipeline.
/// </summary>
public sealed class TestIdentityProvider
{
    public const string Authority = "https://login.example.test/tenant/v2.0";
    public const string Audience = "11111111-1111-1111-1111-111111111111";
    public const string Scope = "api://11111111-1111-1111-1111-111111111111/mcp.access";
    public const string ScopeClaimValue = "mcp.access";
    public const string Role = "Mcp.Access";
    public const string Subject = "user-1";
    public const string UserName = "Test User";

    private readonly RsaSecurityKey _signingKey = CreateKey();

    public TestIdentityProvider()
    {
        Configuration = new OpenIdConnectConfiguration { Issuer = Authority };
        Configuration.SigningKeys.Add(_signingKey);
    }

    public OpenIdConnectConfiguration Configuration { get; }

    public static RsaSecurityKey CreateKey() => new(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString("N") };

    public string CreateToken(
        string audience = Audience,
        string issuer = Authority,
        string? scope = ScopeClaimValue,
        string[]? roles = null,
        bool expired = false,
        SecurityKey? signingKey = null)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = Subject,
            ["name"] = UserName,
        };

        if (scope is not null)
        {
            claims["scp"] = scope;
        }

        if (roles is not null)
        {
            claims["roles"] = roles;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now.AddHours(expired ? -2 : 0),
            NotBefore = now.AddHours(expired ? -2 : 0),
            Expires = now.AddHours(expired ? -1 : 1),
            SigningCredentials = new SigningCredentials(signingKey ?? _signingKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
