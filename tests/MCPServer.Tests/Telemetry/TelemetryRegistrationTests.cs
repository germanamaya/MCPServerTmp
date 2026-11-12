using MCPServer.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace MCPServer.Tests.Telemetry;

public class TelemetryRegistrationTests
{
    private const string ValidConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope-0.in.applicationinsights.azure.com/";

    private static (TelemetryStatus Status, ServiceCollection Services) Register(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TelemetryRegistration.ConnectionStringKey] = connectionString,
            })
            .Build();
        var services = new ServiceCollection();

        return (services.AddServerTelemetry(configuration), services);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddServerTelemetry_ConnectionStringUnset_IsDisabled(string? connectionString)
    {
        var (status, services) = Register(connectionString);

        status.ShouldBe(TelemetryStatus.Disabled);
        services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not-a-connection-string")]
    [InlineData("InstrumentationKey=not-a-guid")]
    [InlineData("IngestionEndpoint=https://westeurope-0.in.applicationinsights.azure.com/")]
    public void AddServerTelemetry_ConnectionStringUnusable_DoesNotRegisterExporter(string connectionString)
    {
        var (status, services) = Register(connectionString);

        status.ShouldBe(TelemetryStatus.InvalidConnectionString);
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddServerTelemetry_ConnectionStringValid_RegistersExporter()
    {
        var (status, services) = Register(ValidConnectionString);

        status.ShouldBe(TelemetryStatus.Enabled);
        services.ShouldNotBeEmpty();
    }
}
