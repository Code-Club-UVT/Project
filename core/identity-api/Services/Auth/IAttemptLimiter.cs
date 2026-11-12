namespace Identity.Services.Auth;

/// <summary>
/// Counts failed logins per identifier and emails per address and in total, in memory (per instance; a restart forgets them).
/// Unknown identifiers are counted exactly like known ones, so the answer never tells whether an account exists.
/// </summary>
public interface IAttemptLimiter
{
    /// <summary>True while the identifier has used up its failed logins.</summary>
    bool IsLoginBlocked(string identifier);

    void RecordLoginFailure(string identifier);

    /// <summary>A successful login starts the count again.</summary>
    void ClearLogin(string identifier);

    /// <summary>
    /// <see cref="EmailPermit.Allowed"/> (and counted) when an email to this address may be sent now. Otherwise nothing is
    /// counted and the caller sends nothing: <see cref="EmailPermit.AddressLimited"/> when it is too soon after the last
    /// email to this address or its hourly limit is reached, <see cref="EmailPermit.GlobalLimited"/> when the service as a
    /// whole has sent its allowed number of emails in the last hour or day.
    /// </summary>
    EmailPermit TryAcquireEmail(string address);
}

public enum EmailPermit
{
    Allowed,
    AddressLimited,
    GlobalLimited
}
