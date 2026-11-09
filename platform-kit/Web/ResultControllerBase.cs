using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlatformKit.Results;

namespace PlatformKit.Web;

/// <summary>
/// Maps service results to HTTP responses: success gives 200 with the value (204 without one), a failure gives a
/// ProblemDetails (RFC 9457) whose status follows the <see cref="ErrorType"/> and which carries the error code as
/// <c>code</c>. Has no route, so each API adds its own (for example <c>api/[controller]</c> or <c>internal/...</c>).
/// </summary>
[ApiController]
public abstract class ResultControllerBase : ControllerBase
{
    protected IActionResult ToActionResult(Result result)
    {
        return result.IsSuccess ? NoContent() : ToErrorResult(result.Error!);
    }

    protected IActionResult ToActionResult<T>(Result<T> result)
    {
        return result.IsSuccess ? Ok(result.Value) : ToErrorResult(result.Error!);
    }

    private ObjectResult ToErrorResult(Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bad Request"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.TooManyRequests => (StatusCodes.Status429TooManyRequests, "Too Many Requests"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title, detail: error.Message);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
