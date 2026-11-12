using System.Globalization;
using System.Net;
using System.Text;
using MCPServer.Mcp;
using MCPServer.Mcp.Tools;
using MCPServer.Tests.Support;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;

namespace MCPServer.Tests.Mcp;

public class McpEndpointTests(McpServerFactory factory) : IClassFixture<McpServerFactory>
{
    private const string ListToolsRequest = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    private async Task<McpClient> ConnectAsync()
    {
        var http = factory.CreateSignedInClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpServerRegistration.EndpointPath),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            loggerFactory: null,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    private static HttpRequestMessage CreateListToolsRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, McpServerRegistration.EndpointPath)
        {
            Content = new StringContent(ListToolsRequest, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }

    [Fact]
    public async Task PostMcp_NoToken_Returns401WithResourceMetadataChallenge()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(CreateListToolsRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.ShouldHaveSingleItem();
        challenge.Scheme.ShouldBe("Bearer");
        challenge.Parameter.ShouldNotBeNull().ShouldContain("/.well-known/oauth-protected-resource/mcp");
    }

    [Fact]
    public async Task PostMcp_TokenWithoutRequiredScope_Returns403()
    {
        using var client = factory.CreateSignedInClient(factory.IdentityProvider.CreateToken(scope: "other.scope"));

        using var response = await client.SendAsync(CreateListToolsRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListTools_SignedIn_ReturnsOnlyTheStarterTools()
    {
        await using var client = await ConnectAsync();

        var tools = await client.ListToolsAsync();

        tools.Select(tool => tool.Name).Order().ShouldBe([EchoTool.Name, CurrentTimeTool.Name]);
    }

    [Fact]
    public async Task Initialize_SignedIn_DoesNotAdvertiseResourcesOrPrompts()
    {
        await using var client = await ConnectAsync();

        client.ServerCapabilities.Tools.ShouldNotBeNull();
        client.ServerCapabilities.Resources.ShouldBeNull();
        client.ServerCapabilities.Prompts.ShouldBeNull();
    }

    [Fact]
    public async Task CallEcho_SignedIn_ReturnsTextUnchanged()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            EchoTool.Name,
            new Dictionary<string, object?> { ["text"] = "Hello, MCP!" });

        result.IsError.ShouldNotBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe("Hello, MCP!");
    }

    [Fact]
    public async Task CallEcho_TextOverMaxLength_ReturnsToolErrorWithReason()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            EchoTool.Name,
            new Dictionary<string, object?> { ["text"] = new string('a', EchoTool.MaxTextLength + 1) });

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldContain("must not exceed");
    }

    [Fact]
    public async Task CallEcho_MissingText_IsRejected()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(EchoTool.Name, new Dictionary<string, object?>());

        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task CallGetCurrentTime_SignedIn_ReturnsCurrentUtcTime()
    {
        await using var client = await ConnectAsync();
        var before = DateTimeOffset.UtcNow;

        var result = await client.CallToolAsync(CurrentTimeTool.Name);

        var text = result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text;
        text.ShouldEndWith("Z");
        var time = DateTimeOffset.ParseExact(text, "O", CultureInfo.InvariantCulture);
        time.ShouldBeInRange(before, DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task Mcp_SessionMethodsOnStatelessServer_Return405(string method)
    {
        using var client = factory.CreateSignedInClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), McpServerRegistration.EndpointPath);
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }
}
