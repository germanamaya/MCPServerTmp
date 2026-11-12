using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace MCPServer.Mcp.Tools;

[McpServerToolType]
public sealed class CurrentTimeTool(TimeProvider timeProvider, ILogger<CurrentTimeTool> logger)
{
    public const string Name = "get_current_time";

    [McpServerTool(Name = Name, Title = "Get current time", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Returns the current UTC date and time in ISO 8601 format.")]
    public string GetCurrentTime(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        logger.LogInformation("Tool {Tool} returned the current time", Name);
        return utcNow;
    }
}
