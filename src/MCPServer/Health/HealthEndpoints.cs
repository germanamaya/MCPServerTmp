namespace MCPServer.Health;

public sealed record HealthStatusResponse(string Status);

public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    private static readonly HealthStatusResponse Ok = new("ok");
    private static readonly HealthStatusResponse Unavailable = new("unavailable");

    public static IServiceCollection AddHealthServices(this IServiceCollection services)
    {
        services.AddSingleton<IReadinessCheck, ServerReadinessCheck>();
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(LivePath, () => Results.Ok(Ok))
            .AllowAnonymous()
            .WithName("GetLiveness")
            .Produces<HealthStatusResponse>();

        endpoints
            .MapGet(ReadyPath, (IReadinessCheck readiness) => readiness.IsReady()
                ? Results.Ok(Ok)
                : Results.Json(Unavailable, statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous()
            .WithName("GetReadiness")
            .Produces<HealthStatusResponse>()
            .Produces<HealthStatusResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }
}
