using MCPServer.Auth;
using Microsoft.Extensions.Options;

namespace MCPServer.Health;

public interface IReadinessCheck
{
    bool IsReady();
}

/// <summary>Ready once the host has started, is not shutting down, and the required settings are valid.</summary>
internal sealed class ServerReadinessCheck(IHostApplicationLifetime lifetime, IOptions<AuthSettings> authSettings)
    : IReadinessCheck
{
    public bool IsReady() =>
        lifetime.ApplicationStarted.IsCancellationRequested
        && !lifetime.ApplicationStopping.IsCancellationRequested
        && authSettings.Value.IsConfigured;
}
