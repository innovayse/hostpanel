namespace Innovayse.Infrastructure.Notifications.Options;

/// <summary>
/// How the SMTP session to the relay is encrypted.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the former <c>UseSsl</c> boolean, which could say that encryption was wanted but not
/// which of the two incompatible kinds -- and whose <c>false</c> could not be told apart from
/// "nobody set this", so the port was left to decide in its place.
/// </para>
/// <para>
/// The member names are the values a deployment writes. Configuration binds an enum by member
/// name, case-insensitively, so <c>SMTP_ENCRYPTION=tls</c>, <c>ssl</c> and <c>none</c> all bind.
/// Anything else fails the bind itself, at startup, naming the key -- it does not fall back to a
/// value nobody chose.
/// </para>
/// </remarks>
public enum SmtpEncryption
{
    /// <summary>
    /// No encryption at all -- the plain SMTP a local mail catcher speaks, conventionally on port
    /// 1025. Never correct against a relay that is given a password.
    /// </summary>
    None = 0,

    /// <summary>
    /// STARTTLS: the session opens in the clear and is upgraded by the <c>STARTTLS</c> command
    /// before any credential is sent. The mail submission standard, conventionally on port 587.
    /// </summary>
    Tls = 1,

    /// <summary>
    /// Implicit TLS: the connection is encrypted before the first SMTP command, with no plaintext
    /// greeting at all. Conventionally port 465 -- but a relay that expects TLS from the first
    /// byte on some other port is stated here rather than guessed from the port number, which is
    /// what the old <c>UseSsl</c> override existed to correct.
    /// </summary>
    Ssl = 2
}
