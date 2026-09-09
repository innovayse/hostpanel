namespace Innovayse.Infrastructure.Notifications.Options;

/// <summary>
/// Configuration options for the outbound SMTP relay used to deliver notification mail.
/// Bound from the "Smtp" section in appsettings.
/// </summary>
/// <remarks>
/// <para>
/// <b>The section as a whole is deliberately not validated at startup</b>, which is where this
/// class differs from the integration options beside it. Every deployed tier fills it from a
/// different overlay and none of them fills all of it, so a rule that refused a half-filled
/// section would refuse to start the production API. A missing host or port surfaces where mail
/// is actually sent instead -- see <see cref="MailKitEmailSender"/>.
/// </para>
/// <para>
/// <b><see cref="Encryption"/> is the one exception, and has to be.</b> It replaced a
/// <c>UseSsl</c> boolean whose absence was harmless only because the port silently decided in its
/// place. There is no such fallback now, and the value an unset enum would otherwise take is
/// <see cref="SmtpEncryption.None"/> -- handing the relay's password to the network in the clear.
/// So it is nullable, "not set" is a state the type can express, and <c>AddInfrastructure</c>
/// refuses to start the process on it. A misspelt value never reaches that check: the binder
/// fails on it first, at startup, naming the key. Declared, not inferred, in both directions.
/// </para>
/// <para>
/// No other property carries a default. The old defaults named <c>localhost:1025</c> and a
/// template <c>noreply@yourdomain.com</c>, which meant a tier that forgot to override them
/// delivered every password reset and invitation nowhere and said nothing about it.
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

    /// <summary>
    /// Gets the SMTP authentication username, bound from <c>Smtp__Username</c>. Empty where the
    /// relay takes no credentials -- a local catcher refuses <c>AUTH</c> outright -- and the
    /// sender then skips authentication rather than offering a blank user.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>Gets the SMTP authentication password. A secret, so it carries no default.</summary>
    public required string Password { get; init; }

    /// <summary>
    /// Gets how the session to the relay is encrypted -- <c>tls</c>, <c>ssl</c> or <c>none</c>.
    /// See <see cref="SmtpEncryption"/> for what each means and which port conventionally carries
    /// it.
    /// </summary>
    /// <remarks>
    /// <b>Nullable on purpose.</b> <see langword="null"/> is "no deployment stated this", which is
    /// a different thing from <see cref="SmtpEncryption.None"/> -- "this deployment stated that
    /// the session is not encrypted" -- and the two must not be confused, because one of them is
    /// a decision and the other is an omission that would send a password in the clear. The
    /// composition root refuses to start on <see langword="null"/>; nothing anywhere substitutes
    /// a value for it.
    /// </remarks>
    public required SmtpEncryption? Encryption { get; init; }

    /// <summary>
    /// Gets the sender identity used in the <c>From</c> header. Filled from the nested
    /// <c>Smtp__From__Address</c> and <c>Smtp__From__Name</c> keys.
    /// </summary>
    public required SmtpFromOptions From { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Encryption"/> was stated by the deployment.
    /// What the composition root checks at startup, so the reason for the refusal is written
    /// beside the property it is about rather than only in the registration.
    /// </summary>
    public bool HasEncryption => Encryption is not null;
}
