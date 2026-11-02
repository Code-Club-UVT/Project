using CoreApi.Common;
using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CoreApi.Services.Seeding;

/// <summary>
/// Creates the first admin from the Seed:Admin settings, if all three values are set and the user does not exist yet.
/// A failure (for example the database is not reachable yet) is logged and never stops the app from starting.
/// </summary>
public class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var admin = options.Value.Admin;
        if (string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrEmpty(admin.Password))
        {
            return;
        }

        if (admin.Password.Length < PasswordRules.MinLength)
        {
            logger.LogWarning("Admin seeding skipped: the password is shorter than {MinLength} characters", PasswordRules.MinLength);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            var username = admin.Username.Trim();
            var email = admin.Email.Trim();
            if (await users.UsernameExistsAsync(username, cancellationToken) || await users.EmailExistsAsync(email, cancellationToken))
            {
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                Role = UserRole.Admin,
                EmailConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now,
                Profile = new UserProfile()
            };
            user.PasswordHash = hasher.HashPassword(user, admin.Password);

            await users.CreateAsync(user, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded admin user {Username}", username);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin seeding skipped: the database is not available or not migrated");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
