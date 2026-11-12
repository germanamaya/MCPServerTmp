using MCPServer.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace MCPServer.Tests.Mcp;

public class CurrentTimeToolTests
{
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private CurrentTimeTool CreateTool() => new(_timeProvider, NullLogger<CurrentTimeTool>.Instance);

    [Fact]
    public void GetCurrentTime_ClockInAnotherOffset_ReturnsUtcIso8601()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 12, 21, 41, 39, 503, TimeSpan.FromHours(1)));

        CreateTool().GetCurrentTime().ShouldBe("2026-11-12T20:41:39.5030000Z");
    }

    [Fact]
    public void GetCurrentTime_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() => CreateTool().GetCurrentTime(cancellation.Token));
        _timeProvider.DidNotReceive().GetUtcNow();
    }
}
