namespace Innovayse.Infrastructure.Notifications.Options;

/// <summary>
/// Configuration options for the outbound SMTP relay used to deliver notification mail.
/// Bound from the "Smtp" section in appsettings.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <b>not</b> validated at startup, which is where this class differs from the
/// integration options beside it. Every deployed tier fills this section from a different
/// place and none of them fills all of it: <c>appsettings.json</c> supplies
/// <see cref="FromEmail"/>, <see cref="FromName"/> and <see cref="UseSsl"/> and deliberately
/// omits <see cref="Host"/> and <see cref="Port"/>; <c>docker-compose.yml</c> supplies the
/// local mail catcher's host and port; <c>docker-compose.prod.yml</c> supplies only
/// <c>Smtp__Host</c>, <c>Smtp__Port</c> and <c>Smtp__Password</c>. A rule that refused a
/// half-filled section would refuse to start the production API, so a missing value surfaces
/// where mail is actually sent instead -- see <see cref="MailKitEmailSender"/>.
/// </para>
/// <para>
/// No property carries a default. The old defaults named <c>localhost:1025</c> and a template
/// <c>noreply@yourdomain.com</c>, which meant a tier that forgot to override them delivered
/// every password reset and invitation nowhere and said nothing about it.
/// </para>
/// </remarks>
public sealed class SmtpOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Smtp";

    /// <summary>Gets the SMTP server hostname or IP address.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the SMTP server port number.</summary>
    public required int Port { get; init; }

    /// <summary>Gets the SMTP authentication username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the SMTP authentication password. A secret, so it carries no default.</summary>
    public required string Password { get; init; }

    /// <summary>Gets the sender email address used in the <c>From</c> header.</summary>
    public required string FromEmail { get; init; }

    /// <summary>Gets the sender display name used in the <c>From</c> header.</summary>
    public required string FromName { get; init; }

    /// <summary>Gets a value indicating whether to use SSL/TLS when connecting to the SMTP server.</summary>
    public bool UseSsl { get; init; } = true;
}
