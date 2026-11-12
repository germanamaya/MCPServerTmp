using MCPServer.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Shouldly;

namespace MCPServer.Tests.Mcp;

public class EchoToolTests
{
    private readonly EchoTool _tool = new(NullLogger<EchoTool>.Instance);

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("  padded  ")]
    [InlineData("line one\nline two — ünïcödé 🚀")]
    public void Echo_TextProvided_ReturnsTextUnchanged(string text)
    {
        _tool.Echo(text).ShouldBe(text);
    }

    [Fact]
    public void Echo_TextAtMaxLength_ReturnsText()
    {
        var text = new string('a', EchoTool.MaxTextLength);

        _tool.Echo(text).ShouldBe(text);
    }

    [Fact]
    public void Echo_TextOverMaxLength_ThrowsMcpException()
    {
        var text = new string('a', EchoTool.MaxTextLength + 1);

        Should.Throw<McpException>(() => _tool.Echo(text))
            .Message.ShouldContain(EchoTool.MaxTextLength.ToString());
    }

    [Fact]
    public void Echo_NullText_ThrowsMcpException()
    {
        Should.Throw<McpException>(() => _tool.Echo(null!));
    }

    [Fact]
    public void Echo_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() => _tool.Echo("hello", cancellation.Token));
    }
}
