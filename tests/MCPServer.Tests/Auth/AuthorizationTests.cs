using System.Security.Claims;
using MCPServer.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace MCPServer.Tests.Auth;

public class AuthSettingsTests
{
    [Fact]
    public void GetConfigurationErrors_AuthorityAndAudienceSet_ReturnsNoErrors()
    {
        var settings = new AuthSettings { Authority = "https://login.example.test/tenant/v2.0", Audiences = ["api"] };

        settings.GetConfigurationErrors().ShouldBeEmpty();
        settings.IsConfigured.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "api", "Authority")]
    [InlineData("  ", "api", "Authority")]
    [InlineData("http://login.example.test", "api", "HTTPS")]
    [InlineData("not-a-url", "api", "HTTPS")]
    [InlineData("https://login.example.test", " ", "Audiences")]
    public void GetConfigurationErrors_InvalidSettings_ReportsTheSetting(string? authority, string audience, string expected)
    {
        var settings = new AuthSettings { Authority = authority, Audiences = [audience] };

        settings.GetConfigurationErrors().ShouldHaveSingleItem().ShouldContain(expected);
        settings.IsConfigured.ShouldBeFalse();
    }
}

public class ScopeOrRoleAuthorizationHandlerTests
{
    private static async Task<bool> IsAuthorizedAsync(AuthSettings settings, params Claim[] claims)
    {
        var requirement = new ScopeOrRoleRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new ScopeOrRoleAuthorizationHandler(
            Options.Create(settings),
            NullLogger<ScopeOrRoleAuthorizationHandler>.Instance).HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task Handle_NoScopesOrRolesConfigured_Succeeds()
    {
        (await IsAuthorizedAsync(new AuthSettings())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("scp", "mcp.access")]
    [InlineData("scp", "profile mcp.access")]
    [InlineData("scope", "api://app/mcp.access")]
    public async Task Handle_TokenCarriesConfiguredScope_Succeeds(string claimType, string claimValue)
    {
        var settings = new AuthSettings { Scopes = ["api://app/mcp.access"] };

        (await IsAuthorizedAsync(settings, new Claim(claimType, claimValue))).ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_TokenCarriesConfiguredRole_Succeeds()
    {
        var settings = new AuthSettings { Scopes = ["api://app/mcp.access"], Roles = ["Mcp.Access"] };

        (await IsAuthorizedAsync(settings, new Claim("roles", "Mcp.Access"))).ShouldBeTrue();
    }

    [Theory]
    [InlineData("scp", "mcp.access.admin")]
    [InlineData("scp", "other")]
    [InlineData("roles", "mcp.access")]
    public async Task Handle_TokenLacksConfiguredScopeAndRole_DoesNotSucceed(string claimType, string claimValue)
    {
        var settings = new AuthSettings { Scopes = ["api://app/mcp.access"], Roles = ["Mcp.Access"] };

        (await IsAuthorizedAsync(settings, new Claim(claimType, claimValue))).ShouldBeFalse();
    }
}
