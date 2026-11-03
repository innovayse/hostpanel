using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Notifications;

/// <summary>Unit tests for the <see cref="EmailLog"/> entity.</summary>
public sealed class EmailLogTests
{
    /// <summary>A delivery key in the stored shape: 64 lower-case hex characters.</summary>
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>The claim moment every test starts from.</summary>
    private static readonly DateTimeOffset Claimed = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>How long a claim is trusted to be in flight in these tests.</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>Create stamps the send time the caller passes and records the outcome.</summary>
    [Fact]
    public void Create_StampsTheSendTime()
    {
        var sentAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        var log = EmailLog.Create("a@example.test", "Hello", "Body", failure: null, sentAt);

        log.SentAt.Should().Be(sentAt);
        log.Success.Should().BeTrue();
        log.Failure.Should().BeNull();
        log.State.Should().Be(EmailDeliveryState.Sent);
        log.IdempotencyKey.Should().BeNull();
    }

    /// <summary>Create with a failure records the row as failed and unsuccessful.</summary>
    [Fact]
    public void Create_WithFailure_IsFailed()
    {
        var log = EmailLog.Create("a@example.test", "Hello", "Body", EmailSendFailure.Timeout, Claimed);

        log.State.Should().Be(EmailDeliveryState.Failed);
        log.Success.Should().BeFalse();
        log.Failure.Should().Be(EmailSendFailure.Timeout);
    }

    /// <summary>Claim records a keyed row in Sending, stamped with the claim moment and not yet successful.</summary>
    [Fact]
    public void Claim_StartsSending()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);

        log.State.Should().Be(EmailDeliveryState.Sending);
        log.IdempotencyKey.Should().Be(Key);
        log.SentAt.Should().Be(Claimed);
        log.Success.Should().BeFalse();
        log.Failure.Should().BeNull();
    }

    /// <summary>MarkDelivered moves a claim to Sent, successful, stamped with the outcome moment.</summary>
    [Fact]
    public void MarkDelivered_IsSent()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        var answered = Claimed.AddSeconds(2);

        log.MarkDelivered(answered);

        log.State.Should().Be(EmailDeliveryState.Sent);
        log.Success.Should().BeTrue();
        log.Failure.Should().BeNull();
        log.SentAt.Should().Be(answered);
    }

    /// <summary>MarkUndelivered moves a claim to Failed with the reason and the outcome moment.</summary>
    [Fact]
    public void MarkUndelivered_IsFailed()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        var answered = Claimed.AddSeconds(2);

        log.MarkUndelivered(EmailSendFailure.RelayUnreachable, answered);

        log.State.Should().Be(EmailDeliveryState.Failed);
        log.Success.Should().BeFalse();
        log.Failure.Should().Be(EmailSendFailure.RelayUnreachable);
        log.SentAt.Should().Be(answered);
    }

    /// <summary>A delivered row may never be sent again, however much time passed.</summary>
    [Fact]
    public void MayResend_Sent_IsFalse()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        log.MarkDelivered(Claimed);

        log.MayResend(Claimed.AddDays(1), Lease).Should().BeFalse();
    }

    /// <summary>A failed row may be sent again at once.</summary>
    [Fact]
    public void MayResend_Failed_IsTrue()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        log.MarkUndelivered(EmailSendFailure.Timeout, Claimed);

        log.MayResend(Claimed, Lease).Should().BeTrue();
    }

    /// <summary>A claim still inside its lease is trusted to be in flight and is not sent again.</summary>
    [Fact]
    public void MayResend_SendingWithinLease_IsFalse()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);

        log.MayResend(Claimed + Lease - TimeSpan.FromSeconds(1), Lease).Should().BeFalse();
    }

    /// <summary>A claim whose lease ran out (the sender died) may be sent again, from the lease's end on.</summary>
    [Fact]
    public void MayResend_SendingPastLease_IsTrue()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);

        log.MayResend(Claimed + Lease, Lease).Should().BeTrue();
    }

    /// <summary>Reclaim puts a failed row back in Sending, clears the reason and stamps the new claim.</summary>
    [Fact]
    public void Reclaim_Failed_StartsSendingAgain()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        log.MarkUndelivered(EmailSendFailure.Timeout, Claimed);
        var again = Claimed.AddMinutes(10);

        log.Reclaim(again);

        log.State.Should().Be(EmailDeliveryState.Sending);
        log.Failure.Should().BeNull();
        log.Success.Should().BeFalse();
        log.SentAt.Should().Be(again);
        log.IdempotencyKey.Should().Be(Key);
    }

    /// <summary>Reclaim refuses a delivered row with the already-delivered code.</summary>
    [Fact]
    public void Reclaim_Sent_Throws()
    {
        var log = EmailLog.Claim("a@example.test", "Hello", "Body", Key, Claimed);
        log.MarkDelivered(Claimed);

        var act = () => log.Reclaim(Claimed.AddMinutes(10));

        act.Should().Throw<DomainRuleException>()
            .Which.Code.Should().Be(NotificationsErrors.EmailAlreadyDelivered);
        log.State.Should().Be(EmailDeliveryState.Sent);
    }

    /// <summary>Backdate moves the send time to the day the message went out and keeps the outcome.</summary>
    [Fact]
    public void Backdate_MovesTheSendTimeOnly()
    {
        var log = EmailLog.Create("a@example.test", "Hello", "Body", EmailSendFailure.RecipientRejected, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        var sent = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

        log.Backdate(sent);

        log.SentAt.Should().Be(sent);
        log.Success.Should().BeFalse();
        log.Failure.Should().Be(EmailSendFailure.RecipientRejected);
    }
}
