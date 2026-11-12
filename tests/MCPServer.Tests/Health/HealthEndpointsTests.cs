using System.Net;
using MCPServer.Auth;
using MCPServer.Health;
using MCPServer.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace MCPServer.Tests.Health;

public class HealthEndpointsTests(McpServerFactory factory) : IClassFixture<McpServerFactory>
{
    [Fact]
    public async Task Live_Anonymous_Returns200Ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(HealthEndpoints.LivePath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"status":"ok"}""");
    }

    [Fact]
    public async Task Ready_ServerStartedAndConfigured_Returns200Ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(HealthEndpoints.ReadyPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"status":"ok"}""");
    }

    [Fact]
    public async Task Ready_NotReady_Returns503WithGenericStatusOnly()
    {
        var readiness = Substitute.For<IReadinessCheck>();
        readiness.IsReady().Returns(false);
        using var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(readiness)))
            .CreateClient();

        using var response = await client.GetAsync(HealthEndpoints.ReadyPath);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"status":"unavailable"}""");
    }
}

public class ServerReadinessCheckTests
{
    private static readonly AuthSettings Configured = new()
    {
        Authority = "https://login.example.test/tenant/v2.0",
        Audiences = ["api"],
    };

    private static bool IsReady(bool started, bool stopping, AuthSettings settings)
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(new CancellationToken(canceled: started));
        lifetime.ApplicationStopping.Returns(new CancellationToken(canceled: stopping));

        return new ServerReadinessCheck(lifetime, Options.Create(settings)).IsReady();
    }

    [Fact]
    public void IsReady_StartedAndConfigured_ReturnsTrue()
    {
        IsReady(started: true, stopping: false, Configured).ShouldBeTrue();
    }

    [Fact]
    public void IsReady_NotYetStarted_ReturnsFalse()
    {
        IsReady(started: false, stopping: false, Configured).ShouldBeFalse();
    }

    [Fact]
    public void IsReady_ShuttingDown_ReturnsFalse()
    {
        IsReady(started: true, stopping: true, Configured).ShouldBeFalse();
    }

    [Fact]
    public void IsReady_SignInNotConfigured_ReturnsFalse()
    {
        IsReady(started: true, stopping: false, new AuthSettings()).ShouldBeFalse();
    }
}
