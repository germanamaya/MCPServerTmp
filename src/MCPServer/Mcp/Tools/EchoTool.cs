using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MCPServer.Mcp.Tools;

[McpServerToolType]
public sealed class EchoTool(ILogger<EchoTool> logger)
{
    public const string Name = "echo";
    public const int MaxTextLength = 10_000;

    [McpServerTool(Name = Name, Title = "Echo", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Returns the provided text unchanged.")]
    public string Echo(
        [Description("The text to return.")] string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (text is null)
        {
            throw new McpException("The 'text' argument is required.");
        }

        if (text.Length > MaxTextLength)
        {
            throw new McpException($"The 'text' argument must not exceed {MaxTextLength} characters.");
        }

        // The text itself is never logged; it may contain sensitive content.
        logger.LogInformation("Tool {Tool} echoed {TextLength} characters", Name, text.Length);
        return text;
    }
}
