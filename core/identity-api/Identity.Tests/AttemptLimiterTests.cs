using Identity.Options;
using Identity.Services.Auth;
using Identity.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests;

public class AttemptLimiterTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly ThrottleOptions _options = new() { LoginFailures = 3, LoginWindowMinutes = 15, EmailMinIntervalSeconds = 120, EmailsPerHour = 3 };
    private readonly AttemptLimiter _limiter;

    public AttemptLimiterTests()
    {
        _limiter = new AttemptLimiter(Opts.Of(_options), _clock, NullLogger<AttemptLimiter>.Instance);
    }

    private void Fail(string identifier, int times)
    {
        for (var i = 0; i < times; i++)
        {
            _limiter.RecordLoginFailure(identifier);
        }
    }

    [Fact]
    public void An_identifier_with_no_failures_is_not_blocked()
    {
        Assert.False(_limiter.IsLoginBlocked("ada"));
    }

    [Fact]
    public void It_blocks_after_the_allowed_number_of_failures()
    {
        Fail("ada", 2);
        Assert.False(_limiter.IsLoginBlocked("ada"));

        Fail("ada", 1);
        Assert.True(_limiter.IsLoginBlocked("ada"));
    }

    [Fact]
    public void Identifiers_are_compared_without_case_or_surrounding_spaces()
    {
        Fail("Ada@Example.org", 3);

        Assert.True(_limiter.IsLoginBlocked(" ada@example.org "));
    }

    [Fact]
    public void Other_identifiers_are_not_affected()
    {
        Fail("ada", 3);

        Assert.False(_limiter.IsLoginBlocked("grace"));
    }

    [Fact]
    public void The_block_ends_when_the_oldest_failure_leaves_the_window()
    {
        Fail("ada", 3);

        _clock.Advance(TimeSpan.FromMinutes(14));
        Assert.True(_limiter.IsLoginBlocked("ada"));

        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(_limiter.IsLoginBlocked("ada"));
    }

    [Fact]
    public void Failures_spread_over_more_than_the_window_do_not_add_up()
    {
        Fail("ada", 2);
        _clock.Advance(TimeSpan.FromMinutes(16));
        Fail("ada", 2);

        Assert.False(_limiter.IsLoginBlocked("ada"));
    }

    [Fact]
    public void A_successful_login_starts_the_count_again()
    {
        Fail("ada", 2);
        _limiter.ClearLogin("ada");
        Fail("ada", 2);

        Assert.False(_limiter.IsLoginBlocked("ada"));
    }

    [Fact]
    public void The_first_email_is_allowed()
    {
        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
    }

    [Fact]
    public void A_second_email_too_soon_is_refused_and_one_after_the_interval_is_allowed()
    {
        _limiter.TryAcquireEmail("ada@example.org");

        _clock.Advance(TimeSpan.FromSeconds(119));
        Assert.Equal(EmailPermit.AddressLimited, _limiter.TryAcquireEmail("ada@example.org"));

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
    }

    [Fact]
    public void A_refused_email_does_not_restart_the_interval()
    {
        _limiter.TryAcquireEmail("ada@example.org");
        _clock.Advance(TimeSpan.FromSeconds(100));
        Assert.Equal(EmailPermit.AddressLimited, _limiter.TryAcquireEmail("ada@example.org"));

        _clock.Advance(TimeSpan.FromSeconds(21));

        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
    }

    [Fact]
    public void The_hourly_limit_stops_further_emails_until_an_hour_has_passed()
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
            _clock.Advance(TimeSpan.FromMinutes(3));
        }

        Assert.Equal(EmailPermit.AddressLimited, _limiter.TryAcquireEmail("ada@example.org"));

        _clock.Advance(TimeSpan.FromMinutes(52));
        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
    }

    [Fact]
    public void The_global_hourly_limit_stops_emails_to_every_address_until_an_hour_has_passed()
    {
        var limiter = new AttemptLimiter(
            Opts.Of(new ThrottleOptions { GlobalEmailsPerHour = 3, GlobalEmailsPerDay = 100 }), _clock, NullLogger<AttemptLimiter>.Instance);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail($"user{i}@example.org"));
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(EmailPermit.GlobalLimited, limiter.TryAcquireEmail("new@example.org"));

        _clock.Advance(TimeSpan.FromMinutes(58));
        Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail("new@example.org"));
    }

    [Fact]
    public void The_global_daily_limit_holds_across_hours()
    {
        var limiter = new AttemptLimiter(
            Opts.Of(new ThrottleOptions { GlobalEmailsPerHour = 2, GlobalEmailsPerDay = 4 }), _clock, NullLogger<AttemptLimiter>.Instance);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail($"user{i}@example.org"));
            _clock.Advance(TimeSpan.FromMinutes(40));
        }

        // 160 minutes in: the last hour holds one email, the last 24 hours all four.
        Assert.Equal(EmailPermit.GlobalLimited, limiter.TryAcquireEmail("new@example.org"));

        _clock.Advance(TimeSpan.FromHours(22));
        Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail("new@example.org"));
    }

    [Fact]
    public void An_email_refused_by_a_limit_is_not_counted_against_the_other_limit()
    {
        var limiter = new AttemptLimiter(
            Opts.Of(new ThrottleOptions { EmailMinIntervalSeconds = 120, GlobalEmailsPerHour = 2, GlobalEmailsPerDay = 100 }), _clock, NullLogger<AttemptLimiter>.Instance);

        Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail("ada@example.org"));
        // Refused for the address: the total still has room for one.
        Assert.Equal(EmailPermit.AddressLimited, limiter.TryAcquireEmail("ada@example.org"));
        Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail("grace@example.org"));

        // Refused for the total: the address was not counted, so it is allowed as soon as the total frees up.
        Assert.Equal(EmailPermit.GlobalLimited, limiter.TryAcquireEmail("new@example.org"));
        _clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(EmailPermit.Allowed, limiter.TryAcquireEmail("new@example.org"));
    }

    [Fact]
    public void Addresses_are_independent_and_compared_without_case()
    {
        _limiter.TryAcquireEmail("ada@example.org");

        Assert.Equal(EmailPermit.AddressLimited, _limiter.TryAcquireEmail("ADA@example.org"));
        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("grace@example.org"));
    }

    [Fact]
    public void Login_and_email_counts_do_not_mix()
    {
        Fail("ada@example.org", 3);

        Assert.Equal(EmailPermit.Allowed, _limiter.TryAcquireEmail("ada@example.org"));
    }

    [Fact]
    public void When_the_table_is_full_new_names_are_not_tracked_until_old_entries_expire()
    {
        for (var i = 0; i < 50_000; i++)
        {
            _limiter.RecordLoginFailure("filler-" + i);
        }

        Fail("late-comer", 3);
        Assert.False(_limiter.IsLoginBlocked("late-comer"));

        _clock.Advance(TimeSpan.FromMinutes(20));
        Fail("late-comer", 3);

        Assert.True(_limiter.IsLoginBlocked("late-comer"));
    }
}
