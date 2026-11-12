using Microsoft.Extensions.Options;

namespace Identity.Tests.Support;

/// <summary>Short for <c>Options.Create</c>: the namespace <c>Identity.Options</c> hides the framework's <c>Options</c> class.</summary>
public static class Opts
{
    public static IOptions<T> Of<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);
}
