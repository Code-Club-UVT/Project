using System.Reflection;
using System.Security.Claims;
using EmailApi.Controllers;
using EmailApi.Extensions;
using EmailApi.Models;
using EmailApi.Options;
using EmailApi.Senders;
using EmailApi.Services;
using EmailApi.Templates;
using EmailApi.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformKit.Internal;
using PlatformKit.Results;
using Xunit;

namespace EmailApi.Tests;

public class InternalEmailsControllerTests
{
    private sealed class FakeProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Detail = detail };
    }

    private sealed class FakeEmailService(Result result) : IEmailService
    {
        public string? Caller { get; private set; }

        public Task<Result> SendAsync(SendEmailRequest request, string caller, CancellationToken cancellationToken = default)
        {
            Caller = caller;
            return Task.FromResult(result);
        }
    }

    private static InternalEmailsController ControllerFor(FakeEmailService service)
    {
        // The same shape the internal key handler builds: the caller's name is the "sub" claim and the name claim.
        var identity = new ClaimsIdentity([new Claim("sub", "identity")], InternalAuth.SchemeName, "sub", "role");
        return new InternalEmailsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
            ProblemDetailsFactory = new FakeProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task A_sent_email_is_a_204_and_the_service_is_told_which_service_asked()
    {
        var service = new FakeEmailService(Result.Success());

        var result = await ControllerFor(service).Send(new SendEmailRequest(), default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("identity", service.Caller);
    }

    [Fact]
    public async Task A_refused_email_is_a_400_with_its_code()
    {
        var service = new FakeEmailService(EmailErrors.UnknownTemplate("nope"));

        var result = await ControllerFor(service).Send(new SendEmailRequest(), default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("Email.UnknownTemplate", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public void The_endpoint_is_internal_only_and_has_no_other_way_in()
    {
        var controller = typeof(InternalEmailsController);

        Assert.Equal(InternalAuth.SchemeName, controller.GetCustomAttributes<AuthorizeAttribute>().Single().AuthenticationSchemes);
        Assert.Equal("internal/emails", controller.GetCustomAttributes<RouteAttribute>().Single().Template);
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());

        var controllers = typeof(InternalEmailsController).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
        Assert.Equal([controller], controllers);
    }
}

public class StartupWiringTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider Build(string environment, Dictionary<string, string?> values)
    {
        var config = Config(values);
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddEmailServices(new TestEnvironment(environment), config);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> Smtp(string? password = "a-real-password", string fromName = "Code Club", bool enabled = true) => new()
    {
        ["Smtp:Enabled"] = enabled.ToString(),
        ["Smtp:Host"] = "smtp.example.org",
        ["Smtp:Port"] = "587",
        ["Smtp:Username"] = "mailer",
        ["Smtp:Password"] = password,
        ["Smtp:FromAddress"] = "no-reply@example.org",
        ["Smtp:FromName"] = fromName
    };

    [Fact]
    public void With_smtp_enabled_the_real_sender_is_used_in_every_environment()
    {
        foreach (var environment in new[] { "Development", "Production" })
        {
            using var provider = Build(environment, Smtp());

            Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IEmailSender>());
        }
    }

    [Fact]
    public void Development_without_smtp_logs_emails_instead()
    {
        using var provider = Build("Development", new() { ["Smtp:FromName"] = "Code Club" });

        Assert.IsType<LoggingEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Outside_development_no_sender_stops_the_app_so_emails_are_never_written_to_a_log()
    {
        Assert.Throws<InvalidOperationException>(() => Build("Production", new() { ["Smtp:FromName"] = "Code Club" }));
        Assert.Throws<InvalidOperationException>(() => Build("Production", Smtp(enabled: false)));
    }

    [Fact]
    public void Both_templates_are_registered()
    {
        using var provider = Build("Development", new() { ["Smtp:FromName"] = "Code Club" });

        Assert.Equal(
            ["email-confirmation", "password-reset"],
            provider.GetServices<IEmailTemplate>().Select(t => t.Name).Order());
    }

    [Theory]
    [InlineData("CHANGE-ME-smtp-password")]
    [InlineData("")]
    [InlineData(null)]
    public void A_placeholder_or_missing_smtp_password_is_refused(string? password)
    {
        using var provider = Build("Production", Smtp(password));

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
        Assert.Contains("Password", string.Join(" ", ex.Failures));
    }

    [Fact]
    public void The_sender_name_is_always_required()
    {
        using var provider = Build("Development", new() { ["Smtp:FromName"] = " " });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }

    [Fact]
    public async Task Every_endpoint_needs_the_internal_key_by_default()
    {
        var config = Config(new() { ["InternalAuth:Callers:identity"] = new string('k', 40) });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInternalOnlyAuth(new TestEnvironment("Production"));
        using var provider = services.BuildServiceProvider();

        var fallback = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Equal([InternalAuth.SchemeName], fallback.AuthenticationSchemes);
        Assert.Contains(fallback.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }
}

public class LoggingEmailSenderTests
{
    private sealed class CapturingLogger : ILogger<LoggingEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task The_development_sender_writes_the_recipient_subject_and_text_to_the_log()
    {
        var logger = new CapturingLogger();
        var content = new EmailContent("Hello", "<p>Hi</p>", "Hi there https://example.org/x");

        await new LoggingEmailSender(logger).SendAsync("ada@example.org", content);

        var line = Assert.Single(logger.Messages);
        Assert.Contains("ada@example.org", line);
        Assert.Contains("Hello", line);
        Assert.Contains("https://example.org/x", line);
    }
}
