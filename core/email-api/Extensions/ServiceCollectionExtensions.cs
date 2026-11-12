using EmailApi.Data.Postgres;
using EmailApi.Options;
using EmailApi.Senders;
using EmailApi.Services;
using EmailApi.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformKit;
using PlatformKit.Internal;

namespace EmailApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PostgresDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("PostgresConnection"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PostgresDbContext.Schema)));
        services.AddScoped<ISentEmailRepository, SentEmailRepository>();

        services.AddPlatformHealth().AddCheck<PostgresHealthCheck>("postgres");

        return services;
    }

    /// <summary>Only other services call this API: the internal key is the one scheme, and every endpoint requires it.</summary>
    public static IServiceCollection AddInternalOnlyAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAuthentication(InternalAuth.SchemeName).AddInternalKeyAuthentication(environment);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(InternalAuth.SchemeName).RequireAuthenticatedUser().Build());

        return services;
    }

    public static IServiceCollection AddEmailServices(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IEmailTemplate, EmailConfirmationTemplate>();
        services.AddSingleton<IEmailTemplate, PasswordResetTemplate>();
        services.AddScoped<IEmailService, EmailService>();

        services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.FromName), "Smtp:FromName must be set (it is the sender name and the app name in every email).")
            .Validate(IsValidSmtp, "Smtp is enabled but Host, Port, Username, Password and FromAddress must be set, and Password must not be the CHANGE-ME placeholder.")
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{SmtpOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            throw new InvalidOperationException("No email sender is configured. Set Smtp__Enabled=true and the other Smtp__* settings.");
        }

        return services;
    }

    private static bool IsValidSmtp(SmtpOptions o) =>
        !o.Enabled
        || (!string.IsNullOrWhiteSpace(o.Host)
            && o.Port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(o.Username)
            && !string.IsNullOrWhiteSpace(o.Password)
            && !o.Password.StartsWith(SmtpOptions.PlaceholderPrefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(o.FromAddress)
            && o.TimeoutSeconds > 0);
}

internal sealed class PostgresHealthCheck(PostgresDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        return await context.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }
}
