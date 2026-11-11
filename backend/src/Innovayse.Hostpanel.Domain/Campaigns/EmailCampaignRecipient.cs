using System.Security.Cryptography;
using System.Text;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// One client an <see cref="EmailCampaign"/> is delivered to, with the address and language the client had when the
/// recipients were fixed. Stored in the <c>EmailCampaignRecipients</c> table.
/// </summary>
public sealed class EmailCampaignRecipient : BaseEntity
{
    /// <summary>Most delivery attempts one recipient gets, the successful one included.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Longest email address the stored column holds.</summary>
    public const int EmailMaxLength = 255;

    /// <summary>Longest language code the stored column holds.</summary>
    public const int LanguageMaxLength = 10;

    /// <summary>Longest failure reason code the stored column holds.</summary>
    public const int FailureReasonMaxLength = 64;

    /// <summary>Random bytes behind each unsubscribe token; the token is their hexadecimal text.</summary>
    private const int UnsubscribeTokenBytes = 32;

    /// <summary>Length of an unsubscribe token as it travels in a mail: two hexadecimal characters per random byte.</summary>
    public const int UnsubscribeTokenLength = UnsubscribeTokenBytes * 2;

    /// <summary>Length of the stored hash of an unsubscribe token: a SHA-256 digest in hexadecimal.</summary>
    public const int UnsubscribeTokenHashLength = 64;

    /// <summary>Gets the FK to the parent <see cref="EmailCampaign"/>.</summary>
    public int CampaignId { get; private set; }

    /// <summary>Gets the id of the client the campaign goes to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the client's email address as it was when the recipients were fixed; a later change of address does not redirect the campaign.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the client's language as it was when the recipients were fixed, or <see langword="null"/> when none was known.</summary>
    public string? Language { get; private set; }

    /// <summary>Gets how the delivery to this client stands.</summary>
    public CampaignRecipientStatus Status { get; private set; }

    /// <summary>Gets how many delivery attempts have been made, the successful one included; never more than <see cref="MaxAttempts"/>.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets when the email was delivered, or <see langword="null"/> until then.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Gets the short stable code of the last failed attempt, or <see langword="null"/> when none failed or the last attempt succeeded. Never wording a mail relay produced.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hash (lower-case hexadecimal) of the secret that names this recipient in the unsubscribe link of its
    /// mail, or <see langword="null"/> until a mail is about to be sent. Only the hash is stored: the token itself exists in
    /// the mail alone, so nobody who can read the database can follow a client's link. See <see cref="HashToken"/>.
    /// </summary>
    public string? UnsubscribeTokenHash { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private EmailCampaignRecipient() { }

    /// <summary>Starts a recipient with nothing delivered yet.</summary>
    /// <param name="clientId">The client the campaign goes to.</param>
    /// <param name="email">The client's email address now.</param>
    /// <param name="language">The client's language now, or <see langword="null"/>.</param>
    /// <returns>A new, pending <see cref="EmailCampaignRecipient"/>.</returns>
    /// <exception cref="ArgumentException">The address is blank or longer than its column, or the language is longer than its column.</exception>
    internal static EmailCampaignRecipient Create(int clientId, string email, string? language)
    {
        var trimmedEmail = email?.Trim();
        if (string.IsNullOrEmpty(trimmedEmail) || trimmedEmail.Length > EmailMaxLength)
        {
            throw new ArgumentException($"A recipient needs an email address of at most {EmailMaxLength} characters.", nameof(email));
        }

        var trimmedLanguage = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        if (trimmedLanguage is not null && trimmedLanguage.Length > LanguageMaxLength)
        {
            throw new ArgumentException($"A language code is at most {LanguageMaxLength} characters.", nameof(language));
        }

        return new EmailCampaignRecipient
        {
            ClientId = clientId,
            Email = trimmedEmail,
            Language = trimmedLanguage,
            Status = CampaignRecipientStatus.Pending,
        };
    }

    /// <summary>
    /// Hashes an unsubscribe token the way it is stored, so the public link can be looked up without the token ever being
    /// kept. The token is 256 random bits, so a plain digest is enough; there is nothing to guess and nothing to stretch.
    /// </summary>
    /// <param name="token">The token as it appears in the link.</param>
    /// <returns>The lower-case hexadecimal SHA-256 digest of the token.</returns>
    /// <exception cref="ArgumentException">The token is blank.</exception>
    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Issues the secret for the unsubscribe link of the mail that is about to be sent and keeps its hash. Each attempt gets a
    /// fresh token, so the mail that is sent after a crash carries a token that works.
    /// </summary>
    /// <returns>The token to put in the mail; it is kept nowhere else.</returns>
    /// <exception cref="DomainRuleException">The recipient is not pending.</exception>
    public string IssueUnsubscribeToken()
    {
        EnsurePending();

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(UnsubscribeTokenBytes));
        UnsubscribeTokenHash = HashToken(token);
        return token;
    }

    /// <summary>Records that the email was delivered. This counts as an attempt.</summary>
    /// <param name="now">The moment of delivery, from the caller's clock.</param>
    /// <exception cref="DomainRuleException">The recipient is not pending.</exception>
    public void MarkSent(DateTimeOffset now)
    {
        EnsurePending();

        Attempts++;
        Status = CampaignRecipientStatus.Sent;
        SentAt = now;
        FailureReason = null;
    }

    /// <summary>
    /// Records a failed attempt. The recipient stays pending, to be tried again, until <see cref="MaxAttempts"/>
    /// attempts have been made; the attempt that reaches the limit makes it <see cref="CampaignRecipientStatus.Failed"/>.
    /// </summary>
    /// <param name="reason">A short stable code for the failure; never the relay's own wording.</param>
    /// <exception cref="ArgumentException">The reason is blank or longer than its column.</exception>
    /// <exception cref="DomainRuleException">The recipient is not pending.</exception>
    public void RecordFailure(string reason)
    {
        EnsurePending();

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > FailureReasonMaxLength)
        {
            throw new ArgumentException($"A failure reason is a code of at most {FailureReasonMaxLength} characters.", nameof(reason));
        }

        Attempts++;
        FailureReason = trimmed;
        if (Attempts >= MaxAttempts)
        {
            Status = CampaignRecipientStatus.Failed;
        }
    }

    /// <summary>
    /// Records that the client withdrew consent to marketing email: a mail still waiting is skipped, and a mail already
    /// delivered counts as the one the client unsubscribed from.
    /// </summary>
    /// <exception cref="DomainRuleException">The delivery failed, or the recipient has already unsubscribed.</exception>
    public void MarkUnsubscribed()
    {
        if (Status == CampaignRecipientStatus.Sent)
        {
            Status = CampaignRecipientStatus.Unsubscribed;
            return;
        }

        EnsurePending();

        Status = CampaignRecipientStatus.Unsubscribed;
    }

    /// <summary>Refuses to change a recipient whose delivery has already ended.</summary>
    /// <exception cref="DomainRuleException">The recipient is not pending.</exception>
    private void EnsurePending()
    {
        if (Status != CampaignRecipientStatus.Pending)
        {
            throw new DomainRuleException(CampaignErrors.RecipientNotPending, $"Only a pending recipient can change; current status is {Status}.");
        }
    }
}
