using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PlatformKit.Results;
using PlatformKit.Web;
using Xunit;

namespace PlatformKit.Tests;

public class ResultControllerBaseTests
{
    private sealed class FakeProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Detail = detail };
    }

    private sealed class TestController : ResultControllerBase
    {
        public TestController()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            ProblemDetailsFactory = new FakeProblemDetailsFactory();
        }

        public IActionResult Plain(Result result) => ToActionResult(result);
        public IActionResult Typed<T>(Result<T> result) => ToActionResult(result);
    }

    [Fact]
    public void A_successful_result_without_a_value_is_204()
    {
        Assert.IsType<NoContentResult>(new TestController().Plain(Result.Success()));
    }

    [Fact]
    public void A_successful_result_with_a_value_is_200_with_the_value()
    {
        var action = new TestController().Typed(Result.Success("hello"));

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Equal("hello", ok.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    public void Each_error_type_has_its_status_and_the_code_as_an_extension(ErrorType type, int status)
    {
        var error = new Error("Thing.Code", "Something happened.", type);

        var action = new TestController().Plain(Result.Failure(error));

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal("Something happened.", problem.Detail);
        Assert.Equal("Thing.Code", problem.Extensions["code"]);
    }

    [Fact]
    public void A_failed_typed_result_is_a_problem_too()
    {
        Result<int> failed = Error.NotFound("X.NotFound", "Missing.");

        var result = Assert.IsType<ObjectResult>(new TestController().Typed(failed));

        Assert.Equal(404, result.StatusCode);
    }
}
