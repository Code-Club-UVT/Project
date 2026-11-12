using Identity.Common;
using Identity.Entities;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Identity.Services.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Services.Seeding;

/// <summary>
/// Creates the first account from the Seed:Admin settings, already confirmed, if all three values are set and the user
/// does not exist yet, and tells the core API about it (registered + confirmed) through the outbox. Admin rights
/// themselves are granted by the core API (see <see cref="SeedAdminOptions"/>). A failure (for example the database is
/// not reachable yet) is logged and never stops the app from starting.
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
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
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
                EmailConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            user.PasswordHash = hasher.HashPassword(user, admin.Password);

            await users.CreateAsync(user, cancellationToken);
            await outbox.UserRegisteredAsync(user.Id, user.Username, user.Email, null, null, cancellationToken);
            await outbox.UserConfirmedAsync(user.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded admin account {Username}", username);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin seeding skipped: the database is not available or not migrated");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
