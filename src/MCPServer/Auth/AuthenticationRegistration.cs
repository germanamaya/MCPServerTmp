using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace MCPServer.Auth;

public static class AuthenticationRegistration
{
    public const string McpAccessPolicy = "McpAccess";

    public static IServiceCollection AddServerAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AuthSettings>().BindConfiguration(AuthSettings.SectionName);
        services.AddSingleton<ICurrentUserService, ClaimsCurrentUserService>();
        services.AddSingleton<IAuthorizationHandler, ScopeOrRoleAuthorizationHandler>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddMcp(_ => { });

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthSettings>>((options, settings) => ConfigureJwtBearer(options, settings.Value));

        services
            .AddOptions<McpAuthenticationOptions>(McpAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthSettings>>((options, settings) => ConfigureResourceMetadata(options, settings.Value));

        var mcpAccess = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new ScopeOrRoleRequirement())
            .Build();

        services
            .AddAuthorizationBuilder()
            .AddPolicy(McpAccessPolicy, mcpAccess)
            .SetFallbackPolicy(mcpAccess);

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, AuthSettings settings)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = TokenClaims.Name;
        options.TokenValidationParameters.RoleClaimType = TokenClaims.Roles;

        // With incomplete settings no authority or audience is registered, so every token is rejected.
        if (!settings.IsConfigured)
        {
            return;
        }

        options.Authority = settings.Authority;
        options.TokenValidationParameters.ValidAudiences =
            settings.Audiences.Where(audience => !string.IsNullOrWhiteSpace(audience)).ToArray();
    }

    private static void ConfigureResourceMetadata(McpAuthenticationOptions options, AuthSettings settings)
    {
        var metadata = new ProtectedResourceMetadata();

        if (settings.IsConfigured)
        {
            metadata.AuthorizationServers.Add(settings.Authority!);
        }

        foreach (var scope in settings.Scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)))
        {
            metadata.ScopesSupported.Add(scope);
        }

        options.ResourceMetadata = metadata;
    }
}
