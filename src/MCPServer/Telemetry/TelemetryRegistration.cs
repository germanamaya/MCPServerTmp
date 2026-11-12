using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace MCPServer.Telemetry;

public enum TelemetryStatus
{
    Disabled,
    Enabled,
    InvalidConnectionString,
}

public static class TelemetryRegistration
{
    public const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Exports logs, traces, and metrics to Application Insights when a usable connection string is set.
    /// Telemetry is optional: without it, or with an unusable value, the server keeps console logging.
    /// </summary>
    public static TelemetryStatus AddServerTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return TelemetryStatus.Disabled;
        }

        if (!IsUsable(connectionString))
        {
            return TelemetryStatus.InvalidConnectionString;
        }

        services.AddOpenTelemetry().UseAzureMonitor(options => options.ConnectionString = connectionString);
        return TelemetryStatus.Enabled;
    }

    internal static bool IsUsable(string connectionString) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Any(pair => pair.Length == 2
                && pair[0].Equals("InstrumentationKey", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(pair[1], out _));
}
