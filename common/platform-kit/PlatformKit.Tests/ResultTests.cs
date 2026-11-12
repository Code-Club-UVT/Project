using PlatformKit.Results;
using Xunit;

namespace PlatformKit.Tests;

public class ResultTests
{
    private static readonly Error Boom = Error.Conflict("Thing.Boom", "It broke.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var result = Result.Failure(Boom);

        Assert.True(result.IsFailure);
        Assert.Same(Boom, result.Error);
    }

    [Fact]
    public void A_value_converts_to_a_successful_result()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void An_error_converts_to_a_failed_result_of_any_kind()
    {
        Result plain = Boom;
        Result<string> typed = Boom;

        Assert.Same(Boom, plain.Error);
        Assert.Same(Boom, typed.Error);
    }

    [Fact]
    public void Reading_the_value_of_a_failed_result_throws()
    {
        Result<string> result = Boom;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.TooManyRequests)]
    public void Each_factory_sets_its_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "m"),
            ErrorType.Unauthorized => Error.Unauthorized("c", "m"),
            ErrorType.Forbidden => Error.Forbidden("c", "m"),
            ErrorType.NotFound => Error.NotFound("c", "m"),
            ErrorType.Conflict => Error.Conflict("c", "m"),
            _ => Error.TooManyRequests("c", "m")
        };

        Assert.Equal(type, error.Type);
        Assert.Equal("c", error.Code);
        Assert.Equal("m", error.Message);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(100, 20, 5)]
    public void Paged_result_counts_its_pages(int total, int pageSize, int pages)
    {
        var page = new PagedResult<int>([], 1, pageSize, total);

        Assert.Equal(pages, page.TotalPages);
    }
}
