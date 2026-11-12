using Microsoft.AspNetCore.Diagnostics;

namespace MCPServer.Errors;

/// <summary>Builds the standard <c>{ "error": { "code", "message", "details" } }</c> response.</summary>
public static class ApiErrors
{
    public static ErrorResponse ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => Create(ErrorCodes.BadRequest, "The request is malformed."),
        StatusCodes.Status401Unauthorized => Create(ErrorCodes.Unauthorized, "Authentication is required."),
        StatusCodes.Status403Forbidden => Create(ErrorCodes.Forbidden, "You do not have permission to perform this action."),
        StatusCodes.Status404NotFound => Create(ErrorCodes.NotFound, "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => Create(ErrorCodes.MethodNotAllowed, "The HTTP method is not supported for this resource."),
        >= 500 => Create(ErrorCodes.InternalError, "An unexpected error occurred."),
        _ => Create(ErrorCodes.BadRequest, "The request could not be processed."),
    };

    public static IResult Result(int statusCode) => Results.Json(ForStatus(statusCode), statusCode: statusCode);

    /// <summary>Adds the standard body to error responses that would otherwise be empty.</summary>
    public static Task WriteStatusCodeAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        return response.WriteAsJsonAsync(ForStatus(response.StatusCode), context.HttpContext.RequestAborted);
    }

    private static ErrorResponse Create(string code, string message) => new(new ErrorBody(code, message));
}
