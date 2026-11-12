using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

/// <summary>Limits per account and per email address, on top of the gateway's limits per IP address.</summary>
public sealed class ThrottleOptions
{
    public const string SectionName = "Throttle";

    /// <summary>Failed logins for one identifier (username or email, known or not) before further logins are refused.</summary>
    [Range(1, 1000)]
    public int LoginFailures { get; set; } = 10;

    [Range(1, 1440)]
    public int LoginWindowMinutes { get; set; } = 15;

    /// <summary>Least time between two emails (confirmation or password reset) to the same address.</summary>
    [Range(0, 3600)]
    public int EmailMinIntervalSeconds { get; set; } = 120;

    /// <summary>Most emails to the same address in one hour.</summary>
    [Range(1, 1000)]
    public int EmailsPerHour { get; set; } = 5;

    /// <summary>
    /// Most emails to all addresses together in one hour. Stops the service from being used to flood other people's
    /// inboxes (and the sending mailbox from being blocked) however many addresses and IP addresses are used.
    /// </summary>
    [Range(1, 100000)]
    public int GlobalEmailsPerHour { get; set; } = 200;

    /// <summary>Most emails to all addresses together in 24 hours. Keep it below the mailbox's own daily sending limit.</summary>
    [Range(1, 1000000)]
    public int GlobalEmailsPerDay { get; set; } = 1000;
}
