using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Campaigns;

/// <summary>Unit tests for the <see cref="EmailCampaignRecipient"/> delivery outcomes, built through a sending campaign.</summary>
public sealed class EmailCampaignRecipientTests
{
    /// <summary>The fixed moment every method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a sending campaign and fixes one recipient on it.</summary>
    /// <returns>A pending recipient.</returns>
    private static EmailCampaignRecipient NewRecipient()
    {
        var campaign = EmailCampaign.Create("Subject", "Body", "admin-1", [new(CampaignFilterType.AllActive, null)]);
        campaign.Schedule(Now.AddHours(1), Now);
        campaign.StartSending();
        return campaign.AddRecipient(5, "client@example.com", "en");
    }

    /// <summary>Builds a sending campaign and tries to fix a recipient with the given address and language.</summary>
    /// <param name="email">The address.</param>
    /// <param name="language">The language.</param>
    /// <returns>The recipient, when it is accepted.</returns>
    private static EmailCampaignRecipient Fix(string email, string? language)
    {
        var campaign = EmailCampaign.Create("Subject", "Body", "admin-1", [new(CampaignFilterType.AllActive, null)]);
        campaign.Schedule(Now.AddHours(1), Now);
        campaign.StartSending();
        return campaign.AddRecipient(5, email, language);
    }

    /// <summary>A new recipient is pending with no attempts and no outcome.</summary>
    [Fact]
    public void New_IsPending_WithNoAttempts()
    {
        var recipient = NewRecipient();

        Assert.Equal(CampaignRecipientStatus.Pending, recipient.Status);
        Assert.Equal(0, recipient.Attempts);
        Assert.Null(recipient.SentAt);
        Assert.Null(recipient.FailureReason);
    }

    /// <summary>The address is trimmed and a blank language becomes null.</summary>
    [Fact]
    public void New_TrimsAddress_AndNormalisesBlankLanguage()
    {
        var recipient = Fix("  client@example.com ", "  ");

        Assert.Equal("client@example.com", recipient.Email);
        Assert.Null(recipient.Language);
    }

    /// <summary>A blank or oversized address, or an oversized language, is refused.</summary>
    [Fact]
    public void New_WithInvalidAddressOrLanguage_Throws()
    {
        Assert.Throws<ArgumentException>(() => Fix("  ", null));
        Assert.Throws<ArgumentException>(() => Fix(new string('a', EmailCampaignRecipient.EmailMaxLength) + "@x.com", null));
        Assert.Throws<ArgumentException>(() => Fix("client@example.com", new string('l', EmailCampaignRecipient.LanguageMaxLength + 1)));
    }

    /// <summary>MarkSent records the delivery and counts it as an attempt.</summary>
    [Fact]
    public void MarkSent_RecordsDelivery_AndCountsTheAttempt()
    {
        var recipient = NewRecipient();

        recipient.MarkSent(Now);

        Assert.Equal(CampaignRecipientStatus.Sent, recipient.Status);
        Assert.Equal(Now, recipient.SentAt);
        Assert.Equal(1, recipient.Attempts);
        Assert.Null(recipient.FailureReason);
    }

    /// <summary>A delivery after an earlier failed attempt clears the failure reason.</summary>
    [Fact]
    public void MarkSent_AfterAFailure_ClearsTheReason()
    {
        var recipient = NewRecipient();
        recipient.RecordFailure("RELAY_TEMPORARY");

        recipient.MarkSent(Now);

        Assert.Null(recipient.FailureReason);
        Assert.Equal(2, recipient.Attempts);
        Assert.Equal(CampaignRecipientStatus.Sent, recipient.Status);
    }

    /// <summary>A failure below the limit keeps the recipient pending with the reason and the attempt counted.</summary>
    [Fact]
    public void RecordFailure_BelowTheLimit_StaysPending()
    {
        var recipient = NewRecipient();

        recipient.RecordFailure("RELAY_TEMPORARY");
        recipient.RecordFailure("RELAY_TEMPORARY");

        Assert.Equal(CampaignRecipientStatus.Pending, recipient.Status);
        Assert.Equal(2, recipient.Attempts);
        Assert.Equal("RELAY_TEMPORARY", recipient.FailureReason);
    }

    /// <summary>The third failed attempt makes the recipient Failed.</summary>
    [Fact]
    public void RecordFailure_AtTheLimit_MarksFailed()
    {
        var recipient = NewRecipient();

        recipient.RecordFailure("RELAY_TEMPORARY");
        recipient.RecordFailure("RELAY_TEMPORARY");
        recipient.RecordFailure("MAILBOX_REJECTED");

        Assert.Equal(EmailCampaignRecipient.MaxAttempts, recipient.Attempts);
        Assert.Equal(CampaignRecipientStatus.Failed, recipient.Status);
        Assert.Equal("MAILBOX_REJECTED", recipient.FailureReason);
    }

    /// <summary>A failed recipient gets no fourth attempt.</summary>
    [Fact]
    public void RecordFailure_AfterTheLimit_ThrowsNotPending()
    {
        var recipient = NewRecipient();
        recipient.RecordFailure("A");
        recipient.RecordFailure("A");
        recipient.RecordFailure("A");

        var ex = Assert.Throws<DomainRuleException>(() => recipient.RecordFailure("A"));

        Assert.Equal(CampaignErrors.RecipientNotPending, ex.Code);
        Assert.Equal(EmailCampaignRecipient.MaxAttempts, recipient.Attempts);
    }

    /// <summary>A blank or oversized reason is refused and no attempt is counted.</summary>
    /// <param name="reason">A blank failure reason.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordFailure_WithBlankReason_Throws(string reason)
    {
        var recipient = NewRecipient();

        Assert.Throws<ArgumentException>(() => recipient.RecordFailure(reason));
        Assert.Throws<ArgumentException>(() => recipient.RecordFailure(new string('r', EmailCampaignRecipient.FailureReasonMaxLength + 1)));

        Assert.Equal(0, recipient.Attempts);
    }

    /// <summary>MarkUnsubscribed skips a pending recipient without an attempt.</summary>
    [Fact]
    public void MarkUnsubscribed_SkipsThePendingRecipient()
    {
        var recipient = NewRecipient();

        recipient.MarkUnsubscribed();

        Assert.Equal(CampaignRecipientStatus.Unsubscribed, recipient.Status);
        Assert.Equal(0, recipient.Attempts);
    }

    /// <summary>A delivered mail can be unsubscribed from, and the recipient then counts as unsubscribed.</summary>
    [Fact]
    public void MarkUnsubscribed_AfterDelivery_RecordsTheUnsubscribe()
    {
        var recipient = NewRecipient();
        recipient.MarkSent(Now);

        recipient.MarkUnsubscribed();

        Assert.Equal(CampaignRecipientStatus.Unsubscribed, recipient.Status);
        Assert.Equal(1, recipient.Attempts);
    }

    /// <summary>A failed delivery cannot be unsubscribed from: the client never received the mail.</summary>
    [Fact]
    public void MarkUnsubscribed_AfterAFailedDelivery_ThrowsNotPending()
    {
        var recipient = NewRecipient();
        recipient.RecordFailure("A");
        recipient.RecordFailure("A");
        recipient.RecordFailure("A");

        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => recipient.MarkUnsubscribed()).Code);
    }

    /// <summary>A new recipient has no unsubscribe token until a mail is about to be sent, so nothing secret exists to leak earlier.</summary>
    [Fact]
    public void New_HasNoUnsubscribeTokenHash()
    {
        Assert.Null(NewRecipient().UnsubscribeTokenHash);
    }

    /// <summary>The issued token is random hexadecimal of the link's length, and only its SHA-256 hash is kept on the recipient.</summary>
    [Fact]
    public void IssueUnsubscribeToken_ReturnsARandomTokenAndKeepsOnlyItsHash()
    {
        var first = NewRecipient();
        var second = NewRecipient();

        var token = first.IssueUnsubscribeToken();

        Assert.Equal(EmailCampaignRecipient.UnsubscribeTokenLength, token.Length);
        Assert.Matches("^[0-9a-f]+$", token);
        Assert.NotEqual(token, second.IssueUnsubscribeToken());
        Assert.Equal(EmailCampaignRecipient.UnsubscribeTokenHashLength, first.UnsubscribeTokenHash!.Length);
        Assert.Equal(EmailCampaignRecipient.HashToken(token), first.UnsubscribeTokenHash);
        Assert.NotEqual(token, first.UnsubscribeTokenHash);
    }

    /// <summary>Issuing again replaces the hash, so the token of a mail that is sent again is the one that works.</summary>
    [Fact]
    public void IssueUnsubscribeToken_Again_ReplacesTheHash()
    {
        var recipient = NewRecipient();
        var first = recipient.IssueUnsubscribeToken();

        var second = recipient.IssueUnsubscribeToken();

        Assert.NotEqual(first, second);
        Assert.Equal(EmailCampaignRecipient.HashToken(second), recipient.UnsubscribeTokenHash);
    }

    /// <summary>A token is issued only for a mail that can still be sent.</summary>
    [Fact]
    public void IssueUnsubscribeToken_ForARecipientWhoseDeliveryEnded_ThrowsNotPending()
    {
        var recipient = NewRecipient();
        recipient.MarkSent(Now);

        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => recipient.IssueUnsubscribeToken()).Code);
    }

    /// <summary>The hash is the lower-case hexadecimal SHA-256 of the token's UTF-8 text, the same for the same token.</summary>
    [Fact]
    public void HashToken_IsTheLowerCaseSha256Hex()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", EmailCampaignRecipient.HashToken("abc"));
        Assert.Equal(EmailCampaignRecipient.HashToken("abc"), EmailCampaignRecipient.HashToken("abc"));
    }

    /// <summary>A blank token has no hash.</summary>
    /// <param name="token">The blank token.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HashToken_Blank_Throws(string token)
    {
        Assert.Throws<ArgumentException>(() => EmailCampaignRecipient.HashToken(token));
    }

    /// <summary>A recipient whose delivery has ended can no longer change.</summary>
    [Fact]
    public void AnyOutcome_AfterTheDeliveryEnded_ThrowsNotPending()
    {
        var sent = NewRecipient();
        sent.MarkSent(Now);
        var unsubscribed = NewRecipient();
        unsubscribed.MarkUnsubscribed();

        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => sent.MarkSent(Now)).Code);
        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => unsubscribed.MarkUnsubscribed()).Code);
        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => unsubscribed.MarkSent(Now)).Code);
        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => unsubscribed.RecordFailure("A")).Code);
    }

    /// <summary>
    /// The tracking token is a secret of its own: it has the shape of an unsubscribe token, only its hash is kept, and it is
    /// neither the unsubscribe token nor its hash, so a pixel address cannot unsubscribe the reader.
    /// </summary>
    [Fact]
    public void IssueTrackingToken_IsASeparateSecretFromTheUnsubscribeToken()
    {
        var recipient = NewRecipient();

        var unsubscribe = recipient.IssueUnsubscribeToken();
        var tracking = recipient.IssueTrackingToken();

        Assert.Equal(EmailCampaignRecipient.TrackingTokenLength, tracking.Length);
        Assert.NotEqual(unsubscribe, tracking);
        Assert.Equal(EmailCampaignRecipient.HashToken(tracking), recipient.TrackingTokenHash);
        Assert.Equal(EmailCampaignRecipient.HashToken(unsubscribe), recipient.UnsubscribeTokenHash);
        Assert.NotEqual(recipient.UnsubscribeTokenHash, recipient.TrackingTokenHash);
    }

    /// <summary>Issuing again replaces the earlier tracking hash, and a recipient whose delivery ended cannot be given a new token.</summary>
    [Fact]
    public void IssueTrackingToken_ReplacesTheHash_AndIsRefusedOnceTheDeliveryEnded()
    {
        var recipient = NewRecipient();
        var first = recipient.IssueTrackingToken();
        var second = recipient.IssueTrackingToken();
        var sent = NewRecipient();
        sent.MarkSent(Now);

        Assert.NotEqual(first, second);
        Assert.Equal(EmailCampaignRecipient.HashToken(second), recipient.TrackingTokenHash);
        Assert.Equal(CampaignErrors.RecipientNotPending, Assert.Throws<DomainRuleException>(() => sent.IssueTrackingToken()).Code);
    }

    /// <summary>A new recipient has no open and no click.</summary>
    [Fact]
    public void New_HasNoOpensAndNoClicks()
    {
        var recipient = NewRecipient();

        Assert.Equal(0, recipient.OpenCount);
        Assert.Null(recipient.FirstOpenedAt);
        Assert.Equal(0, recipient.ClickCount);
        Assert.Null(recipient.FirstClickedAt);
    }

    /// <summary>The first open stamps its moment, and every open counts while the first moment stays.</summary>
    [Fact]
    public void RecordOpen_CountsEveryOpen_AndKeepsTheFirstMoment()
    {
        var recipient = NewRecipient();

        recipient.RecordOpen(Now);
        recipient.RecordOpen(Now.AddHours(2));

        Assert.Equal(2, recipient.OpenCount);
        Assert.Equal(Now, recipient.FirstOpenedAt);
        Assert.Equal(0, recipient.ClickCount);
    }

    /// <summary>The first click stamps its moment, and every click counts while the first moment stays.</summary>
    [Fact]
    public void RecordClick_CountsEveryClick_AndKeepsTheFirstMoment()
    {
        var recipient = NewRecipient();

        recipient.RecordClick(Now);
        recipient.RecordClick(Now.AddMinutes(5));
        recipient.RecordClick(Now.AddMinutes(9));

        Assert.Equal(3, recipient.ClickCount);
        Assert.Equal(Now, recipient.FirstClickedAt);
        Assert.Equal(0, recipient.OpenCount);
    }

    /// <summary>An open and a click are recorded in every status: a mail is read after it was delivered, and after its reader unsubscribed.</summary>
    [Fact]
    public void RecordOpenAndClick_AfterTheDeliveryEnded_AreAllowed()
    {
        var sent = NewRecipient();
        sent.MarkSent(Now);
        var unsubscribed = NewRecipient();
        unsubscribed.MarkSent(Now);
        unsubscribed.MarkUnsubscribed();

        sent.RecordOpen(Now.AddHours(1));
        unsubscribed.RecordClick(Now.AddHours(1));

        Assert.Equal(1, sent.OpenCount);
        Assert.Equal(CampaignRecipientStatus.Sent, sent.Status);
        Assert.Equal(1, unsubscribed.ClickCount);
        Assert.Equal(CampaignRecipientStatus.Unsubscribed, unsubscribed.Status);
    }
}
