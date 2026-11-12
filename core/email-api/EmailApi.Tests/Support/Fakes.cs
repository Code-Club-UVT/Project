using EmailApi.Data.Postgres;
using EmailApi.Entities;
using EmailApi.Senders;
using EmailApi.Templates;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EmailApi.Tests.Support;

public sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
}

public sealed class TestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class FakeSender : IEmailSender
{
    public List<(string To, EmailContent Content)> Sent { get; } = [];

    /// <summary>When set, sending throws it (the mail server is down).</summary>
    public Exception? Failure { get; set; }

    public Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Sent.Add((to, content));
        return Task.CompletedTask;
    }
}

public sealed class FakeSentEmails : ISentEmailRepository
{
    public List<SentEmail> Items { get; } = [];

    /// <summary>When true, the next record attempt loses a race: the id was recorded meanwhile.</summary>
    public bool LoseTheRace { get; set; }

    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(e => e.MessageId == messageId));

    public Task<bool> TryAddAsync(SentEmail email, CancellationToken cancellationToken = default)
    {
        if (LoseTheRace || Items.Any(e => e.MessageId == email.MessageId))
        {
            return Task.FromResult(false);
        }

        Items.Add(email);
        return Task.FromResult(true);
    }
}
