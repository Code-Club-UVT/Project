using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace PlatformKit.Tests.Support;

public sealed class TestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
