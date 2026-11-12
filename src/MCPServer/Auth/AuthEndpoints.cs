using System.Security.Claims;
using MCPServer.Errors;

namespace MCPServer.Auth;

public static class AuthEndpoints
{
    public const string CurrentUserPath = "/auth/me";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(CurrentUserPath, GetCurrentUser)
            .RequireAuthorization(AuthenticationRegistration.McpAccessPolicy)
            .WithName("GetCurrentUser")
            .Produces<CurrentUser>()
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static IResult GetCurrentUser(ClaimsPrincipal principal, ICurrentUserService currentUserService)
    {
        var user = currentUserService.GetCurrentUser(principal);
        return user is null
            ? ApiErrors.Result(StatusCodes.Status401Unauthorized)
            : Results.Ok(user);
    }
}
