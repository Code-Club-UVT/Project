namespace Gateway.Pipeline;

/// <summary>Error responses written by the gateway itself (not by a downstream API), as ProblemDetails with a <c>code</c>.</summary>
public static class GatewayProblems
{
    public static Task WriteAsync(HttpContext context, int status, string code, string detail)
    {
        var title = status switch
        {
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            _ => "Error"
        };

        return Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context);
    }
}
