namespace Innovayse.Infrastructure.Integrations.Telegram.Options;

/// <summary>
/// Configuration options for the Telegram bot the public contact form's enquiries are posted to.
/// Bound from the "Telegram" section in appsettings.
/// </summary>
/// <remarks>
/// <para>
/// Telegram is optional in the same way Stripe and Name.am are: a deployment that runs no bot
/// configures none of this, and the contact form works exactly as it did before, delivering by
/// mail alone. A partly filled section is a different thing -- a misconfiguration, because a
/// token with no chat id can post nowhere -- and <see cref="IsUsable"/> is what the composition
/// root checks to refuse it at startup rather than at the first enquiry.
/// </para>
/// <para>
/// These two values are the last credentials that lived in the Nuxt container's runtime config.
/// They moved for the same reason the SMTP password did: the public site had no other reason to
/// hold a secret, and one held there is one templating mistake away from being served.
/// </para>
/// </remarks>
public sealed class TelegramOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Telegram";

    /// <summary>
    /// The bot token issued by BotFather, in its <c>&lt;bot-id&gt;:&lt;secret&gt;</c> form. A
    /// secret, so it carries no default: a fabricated one fails against the live API with a 401
    /// that names nothing about configuration. Empty means no bot is configured.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// The chat an enquiry is posted to -- a numeric id (negative for a group) or an
    /// <c>@channelusername</c>. Kept as a string because both spellings are valid and Telegram
    /// accepts either in the same field. Empty means no bot is configured.
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the whole section was left unset -- a deployment that posts no enquiries to chat,
    /// which is allowed.
    /// </summary>
    public bool IsAbsent => BotToken.Length == 0 && ChatId.Length == 0;

    /// <summary>Whether both values a Telegram post needs are present.</summary>
    public bool IsConfigured => BotToken.Length > 0 && ChatId.Length > 0;

    /// <summary>
    /// Whether this section is in a state the process may start with -- entirely unset, or
    /// complete. Half-filled is neither: a token with no chat id posts nowhere while looking
    /// configured, which is the shape of failure this feature was moved to stop repeating.
    /// </summary>
    public bool IsUsable => IsAbsent || IsConfigured;
}
