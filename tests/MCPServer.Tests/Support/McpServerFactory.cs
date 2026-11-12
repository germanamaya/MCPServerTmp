using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace MCPServer.Tests.Support;

/// <summary>Hosts the real server in memory with sign-in pointed at <see cref="TestIdentityProvider"/>.</summary>
public class McpServerFactory : WebApplicationFactory<Program>
{
    public TestIdentityProvider IdentityProvider { get; } = new();

    protected virtual IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["Authentication:Authority"] = TestIdentityProvider.Authority,
        ["Authentication:Audiences:0"] = TestIdentityProvider.Audience,
        ["Authentication:Scopes:0"] = TestIdentityProvider.Scope,
        ["Authentication:Roles:0"] = TestIdentityProvider.Role,
    };

    public HttpClient CreateSignedInClient(string? token = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token ?? IdentityProvider.CreateToken());
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(IdentityProvider.Configuration)));
    }
}

/// <summary>The server as deployed without any sign-in settings.</summary>
public sealed class UnconfiguredMcpServerFactory : McpServerFactory
{
    protected override IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["Authentication:Authority"] = "",
    };
}
