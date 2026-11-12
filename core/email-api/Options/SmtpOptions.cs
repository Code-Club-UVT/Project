namespace EmailApi.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public const string PlaceholderPrefix = "CHANGE-ME";

    /// <summary>When false, Development falls back to the logging sender and other environments refuse to start.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>true: STARTTLS on a plain connection (port 587). false: implicit TLS (port 465).</summary>
    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Sender name, also shown as the app name in the email header.</summary>
    public string FromName { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;
}
