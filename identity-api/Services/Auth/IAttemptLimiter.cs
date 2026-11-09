namespace Identity.Services.Auth;

/// <summary>
/// Counts failed logins per identifier and emails per address, in memory (per instance; a restart forgets them).
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
    /// True and counted when an email to this address is allowed now; false when it is too soon after the last one or
    /// the hourly limit is reached (the caller then sends nothing and still answers as if it had).
    /// </summary>
    bool TryAcquireEmail(string address);
}
