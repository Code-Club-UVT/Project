using System.Net.Http.Headers;
using System.Text;
using Identity.Entities;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Services.Outbox;

/// <summary>
/// Delivers the outbox: every <see cref="OutboxOptions.PollSeconds"/> it claims the due messages and POSTs each one to
/// its target. A 2xx deletes the message (its body can hold a raw emailed token). Anything else, including a timeout,
/// schedules a retry with exponential backoff; after <see cref="OutboxOptions.MaxAttempts"/> the message is marked failed.
/// Receivers must be idempotent: a message can arrive more than once (for example when the response was lost).
/// </summary>
public class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollSeconds), timeProvider);
        do
        {
            try
            {
                // Keep going while full batches come back, so a backlog drains without waiting for the timer.
                while (await DispatchBatchAsync(stoppingToken) == _options.BatchSize)
                {
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Database down or not migrated yet: try again on the next tick.
                logger.LogWarning(ex, "Outbox dispatch failed; retrying in {Seconds} s", _options.PollSeconds);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> DispatchBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var messages = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batch = await messages.ClaimDueAsync(now, now.AddSeconds(_options.LeaseSeconds), _options.BatchSize, stoppingToken);

        foreach (var message in batch)
        {
            var error = await DeliverAsync(message, stoppingToken);
            if (error is null)
            {
                await messages.DeleteAsync(message, stoppingToken);
            }
            else
            {
                ScheduleRetry(message, error);
                await messages.UpdateAsync(message, stoppingToken);
            }

            // Saved per message, so a crash halfway through a batch does not redeliver the ones already done.
            await unitOfWork.SaveChangesAsync(stoppingToken);
        }

        return batch.Count;
    }

    /// <summary>Returns null on success, otherwise a short description of what went wrong.</summary>
    private async Task<string?> DeliverAsync(OutboxMessage message, CancellationToken stoppingToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(message.Target);
            using var request = new HttpRequestMessage(HttpMethod.Post, message.Path)
            {
                Content = new StringContent(message.Body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", message.Id.ToString());

            using var response = await client.SendAsync(request, stoppingToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Outbox {Type} {MessageId} delivered to {Target}", message.Type, message.Id, message.Target);
                return null;
            }

            return $"HTTP {(int)response.StatusCode} from {message.Target}";
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Includes the client timeout (TaskCanceledException) and connection errors.
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private void ScheduleRetry(OutboxMessage message, string error)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        message.Attempts++;
        message.LastError = error.Length > 1000 ? error[..1000] : error;

        if (message.Attempts >= _options.MaxAttempts)
        {
            message.FailedAt = now;
            logger.LogError("Outbox {Type} {MessageId} to {Target} failed {Attempts} times and was given up: {Error}",
                message.Type, message.Id, message.Target, message.Attempts, error);
            return;
        }

        var backoff = Math.Min(_options.FirstBackoffSeconds * Math.Pow(2, message.Attempts - 1), _options.MaxBackoffSeconds);
        message.NextAttemptAt = now.AddSeconds(backoff);
        logger.LogWarning("Outbox {Type} {MessageId} to {Target} failed (attempt {Attempts}): {Error}; next try in {Seconds} s",
            message.Type, message.Id, message.Target, message.Attempts, error, backoff);
    }
}
