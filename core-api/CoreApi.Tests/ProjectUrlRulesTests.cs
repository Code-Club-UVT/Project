using CoreApi.Services.Projects;
using Xunit;

namespace CoreApi.Tests;

public class ProjectUrlRulesTests
{
    [Theory]
    [InlineData("http://mock-api:8080/api")]
    [InlineData("https://example.org")]
    [InlineData("http://core-api:8080/")]
    [InlineData("http://10.0.0.5:8080")]
    [InlineData("http://172.17.0.2:8080")]
    [InlineData("http://192.168.1.10")]
    public void Ordinary_service_addresses_are_accepted_including_private_ranges(string url)
    {
        Assert.Null(ProjectUrlRules.Validate(url, "BackendUrl", allowLocalAddresses: false));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("mock-api:8080")]
    [InlineData("ftp://example.org/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void Only_absolute_http_and_https_urls_are_accepted(string url)
    {
        var error = ProjectUrlRules.Validate(url, "BackendUrl", allowLocalAddresses: true);

        Assert.NotNull(error);
        Assert.Equal("Project.InvalidUrl", error.Code);
        Assert.Contains("BackendUrl", error.Message);
    }

    [Theory]
    [InlineData("http://user:pass@example.org")]
    [InlineData("http://example.org/?token=1")]
    [InlineData("http://example.org/#section")]
    public void Credentials_a_query_and_a_fragment_are_refused(string url)
    {
        Assert.NotNull(ProjectUrlRules.Validate(url, "FrontendUrl", allowLocalAddresses: true));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://LOCALHOST")]
    [InlineData("http://app.localhost")]
    [InlineData("http://metadata.google.internal/computeMetadata")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://127.1")]
    [InlineData("http://[::1]:8080")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://[::]")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://169.254.0.1")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[::ffff:127.0.0.1]")]
    [InlineData("http://[::ffff:169.254.169.254]")]
    public void Loopback_link_local_and_metadata_addresses_are_refused_outside_development(string url)
    {
        var error = ProjectUrlRules.Validate(url, "BackendUrl", allowLocalAddresses: false);

        Assert.NotNull(error);
        Assert.Contains("loopback", error.Message);
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:8080")]
    public void The_same_addresses_are_allowed_in_development(string url)
    {
        Assert.Null(ProjectUrlRules.Validate(url, "BackendUrl", allowLocalAddresses: true));
    }
}
