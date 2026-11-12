using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MCPServer.Auth;
using MCPServer.Tests.Support;
using Shouldly;

namespace MCPServer.Tests.Auth;

public class BearerTokenAuthenticationTests(McpServerFactory factory) : IClassFixture<McpServerFactory>
{
    private TestIdentityProvider IdentityProvider => factory.IdentityProvider;

    [Fact]
    public async Task CurrentUser_ValidToken_Returns200WithCaller()
    {
        using var client = factory.CreateSignedInClient();

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        user.GetProperty("id").GetString().ShouldBe(TestIdentityProvider.Subject);
        user.GetProperty("name").GetString().ShouldBe(TestIdentityProvider.UserName);
        user.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString())
            .ShouldBe([TestIdentityProvider.ScopeClaimValue]);
    }

    [Fact]
    public async Task CurrentUser_NoToken_Returns401WithStandardError()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("UNAUTHORIZED");
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    public static TheoryData<string, Func<TestIdentityProvider, string>> RejectedTokens => new()
    {
        { "expired", provider => provider.CreateToken(expired: true) },
        { "wrong audience", provider => provider.CreateToken(audience: "22222222-2222-2222-2222-222222222222") },
        { "wrong issuer", provider => provider.CreateToken(issuer: "https://login.example.test/other-tenant/v2.0") },
        { "unknown signing key", provider => provider.CreateToken(signingKey: TestIdentityProvider.CreateKey()) },
        { "not a token", _ => "not-a-token" },
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task CurrentUser_InvalidToken_Returns401(string reason, Func<TestIdentityProvider, string> createToken)
    {
        using var client = factory.CreateSignedInClient(createToken(IdentityProvider));

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, reason);
    }

    [Fact]
    public async Task CurrentUser_TokenWithoutAcceptedScopeOrRole_Returns403WithStandardError()
    {
        using var client = factory.CreateSignedInClient(IdentityProvider.CreateToken(scope: "other.scope"));

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task CurrentUser_AppOnlyTokenWithAcceptedRole_Returns200()
    {
        var token = IdentityProvider.CreateToken(scope: null, roles: [TestIdentityProvider.Role]);
        using var client = factory.CreateSignedInClient(token);

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Anonymous_AdvertisesAuthorityAndScopes()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>();
        metadata.GetProperty("resource").GetString().ShouldNotBeNull().ShouldEndWith("/mcp");
        metadata.GetProperty("authorization_servers").EnumerateArray().Select(server => server.GetString())
            .ShouldBe([TestIdentityProvider.Authority]);
        metadata.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString())
            .ShouldBe([TestIdentityProvider.Scope]);
    }
}

public class UnconfiguredSignInTests(UnconfiguredMcpServerFactory factory) : IClassFixture<UnconfiguredMcpServerFactory>
{
    [Fact]
    public async Task CurrentUser_OtherwiseValidToken_Returns401()
    {
        using var client = factory.CreateSignedInClient();

        using var response = await client.GetAsync(AuthEndpoints.CurrentUserPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ready_SignInNotConfigured_Returns503()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(MCPServer.Health.HealthEndpoints.ReadyPath);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
