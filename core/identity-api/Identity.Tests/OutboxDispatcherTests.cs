using System.Net;
using Identity.Entities;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Identity.Services.Outbox;
using Identity.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests;

/// <summary>
/// The dispatcher delivers one batch as soon as it starts, before it waits for its timer, so each test starts it, waits for
/// that batch to be saved, and stops it.
/// </summary>
public class OutboxDispatcherTests
{
    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public string? AskedFor { get; private set; }

        public HttpClient CreateClient(string name)
        {
            AskedFor = name;
            return client;
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string Body, string? Key)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("Idempotency-Key", out var key);
            Calls.Add((request.Method, request.RequestUri!.ToString(), body, key?.SingleOrDefault()));
            return respond(request);
        }
    }

    private readonly FakeTimeProvider _clock = new();
    private readonly FakeOutboxRepository _messages = new();
    private readonly FakeUnitOfWork _unit = new();
    private readonly OutboxOptions _options = new() { PollSeconds = 60, BatchSize = 20, MaxAttempts = 5, FirstBackoffSeconds = 2, MaxBackoffSeconds = 600, LeaseSeconds = 60 };

    private OutboxMessage Add(string target = "core", int attempts = 0)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Target = target,
            Type = "user-registered",
            Path = "internal/users",
            Body = "{\"hello\":1}",
            CreatedAt = _clock.UtcNow,
            NextAttemptAt = _clock.UtcNow,
            Attempts = attempts
        };
        _messages.Items.Add(message);
        return message;
    }

    private async Task<(RecordingHandler Handler, SingleClientFactory Factory)> RunOneBatchAsync(Func<HttpRequestMessage, HttpResponseMessage> respond, int expectedSaves)
    {
        var handler = new RecordingHandler(respond);
        var factory = new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("http://core-api:8080/") });

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxRepository>(_messages);
        services.AddSingleton<IUnitOfWork>(_unit);
        using var provider = services.BuildServiceProvider();

        var done = new TaskCompletionSource();
        _unit.OnSaved = () =>
        {
            if (_unit.Saves >= expectedSaves)
            {
                done.TrySetResult();
            }
        };

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), factory, Opts.Of(_options), _clock, NullLogger<OutboxDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.StopAsync(CancellationToken.None);

        return (handler, factory);
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    [Fact]
    public async Task A_delivered_message_is_posted_with_its_id_as_the_idempotency_key_and_then_deleted()
    {
        var message = Add("core");

        var (handler, factory) = await RunOneBatchAsync(_ => Status(HttpStatusCode.NoContent), expectedSaves: 1);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("http://core-api:8080/internal/users", call.Url);
        Assert.Equal("{\"hello\":1}", call.Body);
        Assert.Equal(message.Id.ToString(), call.Key);
        Assert.Equal("core", factory.AskedFor);
        Assert.Empty(_messages.Items);
    }

    [Fact]
    public async Task Each_message_goes_to_the_client_of_its_target()
    {
        Add("email");

        var (_, factory) = await RunOneBatchAsync(_ => Status(HttpStatusCode.OK), expectedSaves: 1);

        Assert.Equal("email", factory.AskedFor);
    }

    [Fact]
    public async Task A_failed_delivery_is_kept_and_retried_after_the_first_backoff()
    {
        var message = Add();

        await RunOneBatchAsync(_ => Status(HttpStatusCode.InternalServerError), expectedSaves: 1);

        Assert.Same(message, Assert.Single(_messages.Items));
        Assert.Equal(1, message.Attempts);
        Assert.Equal("HTTP 500 from core", message.LastError);
        Assert.Equal(_clock.UtcNow.AddSeconds(2), message.NextAttemptAt);
        Assert.Null(message.FailedAt);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 4)]
    [InlineData(3, 16)]
    [InlineData(8, 512)]
    [InlineData(9, 600)]
    public async Task The_wait_doubles_on_each_failure_up_to_the_maximum(int attemptsBefore, int expectedSeconds)
    {
        _options.MaxAttempts = 100;
        var message = Add(attempts: attemptsBefore);

        await RunOneBatchAsync(_ => Status(HttpStatusCode.BadGateway), expectedSaves: 1);

        Assert.Equal(attemptsBefore + 1, message.Attempts);
        Assert.Equal(_clock.UtcNow.AddSeconds(expectedSeconds), message.NextAttemptAt);
    }

    [Fact]
    public async Task After_the_maximum_attempts_the_message_is_marked_failed_and_kept()
    {
        var message = Add(attempts: _options.MaxAttempts - 1);

        await RunOneBatchAsync(_ => Status(HttpStatusCode.ServiceUnavailable), expectedSaves: 1);

        Assert.Equal(_options.MaxAttempts, message.Attempts);
        Assert.Equal(_clock.UtcNow, message.FailedAt);
        Assert.Contains(message, _messages.Items);
    }

    [Fact]
    public async Task A_connection_error_counts_as_a_failure_too()
    {
        var message = Add();

        await RunOneBatchAsync(_ => throw new HttpRequestException("connection refused"), expectedSaves: 1);

        Assert.Equal(1, message.Attempts);
        Assert.Contains("HttpRequestException", message.LastError);
    }

    [Fact]
    public async Task A_long_error_is_cut_to_a_thousand_characters()
    {
        var message = Add();

        await RunOneBatchAsync(_ => throw new HttpRequestException(new string('x', 5000)), expectedSaves: 1);

        Assert.Equal(1000, message.LastError!.Length);
    }

    [Fact]
    public async Task Messages_that_are_not_due_yet_are_left_alone()
    {
        var waiting = Add();
        waiting.NextAttemptAt = _clock.UtcNow.AddMinutes(5);
        var due = Add();

        var (handler, _) = await RunOneBatchAsync(_ => Status(HttpStatusCode.OK), expectedSaves: 1);

        Assert.Single(handler.Calls);
        Assert.Equal([waiting], _messages.Items);
        Assert.DoesNotContain(due, _messages.Items);
    }

    [Fact]
    public async Task Every_message_of_the_batch_is_saved_on_its_own()
    {
        Add();
        Add();
        Add();

        await RunOneBatchAsync(_ => Status(HttpStatusCode.OK), expectedSaves: 3);

        Assert.Equal(3, _unit.Saves);
        Assert.Empty(_messages.Items);
    }
}
