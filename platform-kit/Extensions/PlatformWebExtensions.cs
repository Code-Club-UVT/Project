using Microsoft.Extensions.DependencyInjection;
using PlatformKit.Web;

namespace PlatformKit;

public static class PlatformWebExtensions
{
    /// <summary>
    /// ProblemDetails for every error response and a handler that turns unhandled exceptions into a 500 ProblemDetails
    /// (the message is shown only in Development). Also needs <c>app.UseExceptionHandler()</c> first in the pipeline.
    /// </summary>
    public static IServiceCollection AddPlatformProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
