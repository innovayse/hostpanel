namespace Innovayse.Infrastructure.Notifications;

/// <summary>
/// Configuration settings for the SMTP email sender, bound from the <c>Smtp</c> configuration section.
/// </summary>
public sealed class SmtpSettings
{
    /// <summary>Gets the SMTP server hostname or IP address.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the SMTP server port number.</summary>
    public required int Port { get; init; }

    /// <summary>Gets the SMTP authentication username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the SMTP authentication password.</summary>
    public required string Password { get; init; }

    /// <summary>Gets the sender email address used in the <c>From</c> header.</summary>
    public required string FromEmail { get; init; }

    /// <summary>Gets the sender display name used in the <c>From</c> header.</summary>
    public required string FromName { get; init; }

    /// <summary>Gets a value indicating whether to use SSL/TLS when connecting to the SMTP server.</summary>
    public bool UseSsl { get; init; } = true;
}
