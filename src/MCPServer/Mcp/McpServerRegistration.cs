using MCPServer.Auth;
using MCPServer.Mcp.Tools;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace MCPServer.Mcp;

public static class McpServerRegistration
{
    public const string EndpointPath = "/mcp";

    public static IServiceCollection AddMcpServerWithTools(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "MCPServer", Version = "1.0.0" };
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .AddAuthorizationFilters()
            .WithTools<EchoTool>()
            .WithTools<CurrentTimeTool>();

        return services;
    }

    public static IEndpointRouteBuilder MapMcpServer(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapMcp(EndpointPath)
            .RequireAuthorization(AuthenticationRegistration.McpAccessPolicy);

        return endpoints;
    }
}
