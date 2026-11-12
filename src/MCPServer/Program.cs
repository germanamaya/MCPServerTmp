using MCPServer.Auth;
using MCPServer.Errors;
using MCPServer.Health;
using MCPServer.Mcp;
using MCPServer.Telemetry;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var telemetryStatus = builder.Services.AddServerTelemetry(builder.Configuration);

builder.Services.AddHttpLogging(options =>
{
    // Headers, query strings, and bodies stay out of the logs: they can carry credentials.
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddServerAuthentication();
builder.Services.AddHealthServices();
builder.Services.AddMcpServerWithTools();
builder.Services.AddOpenApi();

var app = builder.Build();

if (telemetryStatus == TelemetryStatus.InvalidConnectionString)
{
    app.Logger.LogWarning(
        "{Setting} is set but is not a valid connection string. Application Insights export is disabled.",
        TelemetryRegistration.ConnectionStringKey);
}

foreach (var error in app.Services.GetRequiredService<IOptions<AuthSettings>>().Value.GetConfigurationErrors())
{
    app.Logger.LogError(
        "Sign-in is not configured: {Error} All MCP requests are rejected and readiness reports unavailable.",
        error);
}

app.UseHttpLogging();
app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(ApiErrors.WriteStatusCodeAsync);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMcpServer();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.Run();

public partial class Program;
