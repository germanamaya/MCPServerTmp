using System.Text.Json;
using MCPServer.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace MCPServer.Tests.Errors;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int StatusCode, JsonElement Error, string Body)> HandleAsync(Exception exception)
    {
        using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = body },
        };

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        var text = System.Text.Encoding.UTF8.GetString(body.ToArray());
        return (context.Response.StatusCode, JsonDocument.Parse(text).RootElement.GetProperty("error"), text);
    }

    [Fact]
    public async Task TryHandle_UnexpectedException_Returns500WithoutExceptionDetails()
    {
        var (statusCode, error, body) = await HandleAsync(new InvalidOperationException("secret connection detail"));

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        error.GetProperty("code").GetString().ShouldBe(ErrorCodes.InternalError);
        body.ShouldNotContain("secret connection detail");
        body.ShouldNotContain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task TryHandle_BadHttpRequest_Returns400BadRequest()
    {
        var (statusCode, error, _) = await HandleAsync(new BadHttpRequestException("Malformed body."));

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
        error.GetProperty("code").GetString().ShouldBe(ErrorCodes.BadRequest);
    }
}
