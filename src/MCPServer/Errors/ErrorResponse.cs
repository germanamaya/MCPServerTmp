namespace MCPServer.Errors;

public static class ErrorCodes
{
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string InternalError = "INTERNAL_ERROR";
}

public sealed record ErrorResponse(ErrorBody Error);

public sealed record ErrorBody(string Code, string Message, object? Details = null);
