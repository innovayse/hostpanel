using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The <see cref="InvoicePaymentClaim"/> lifecycle: opened before the plugin is called, ended once by the plugin's
/// answer or the key lookup, never changed after it ended, and stale only after the wait.
/// </summary>
public sealed class InvoicePaymentClaimTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an open claim for invoice 7 of client 3.</summary>
    /// <returns>A new open claim.</returns>
    private static InvoicePaymentClaim NewClaim()
    {
        return InvoicePaymentClaim.Open(7, 3, "bank-gateway", "pm-1", "inv7-key", 1000, "USD", Now);
    }

    /// <summary>A new claim is open, unresolved and carries what the plugin will be asked.</summary>
    [Fact]
    public void Open_StartsUnresolvedWithTheAttempt()
    {
        var claim = NewClaim();

        claim.Status.Should().Be(InvoicePaymentClaimStatus.Open);
        claim.IsUnresolved.Should().BeTrue();
        claim.InvoiceId.Should().Be(7);
        claim.ClientId.Should().Be(3);
        claim.GatewayModuleId.Should().Be("bank-gateway");
        claim.MethodRef.Should().Be("pm-1");
        claim.IdempotencyKey.Should().Be("inv7-key");
        claim.AmountMinor.Should().Be(1000);
        claim.Currency.Should().Be("USD");
        claim.CreatedAt.Should().Be(Now);
        claim.ResolvedAt.Should().BeNull();
    }

    /// <summary>A key longer than the capability allows, or an amount that is not positive, cannot start a claim.</summary>
    [Fact]
    public void Open_WithAnInvalidKeyOrAmount_Throws()
    {
        var longKey = () => InvoicePaymentClaim.Open(7, 3, "m", "pm", new string('k', 65), 1000, "USD", Now);
        var noAmount = () => InvoicePaymentClaim.Open(7, 3, "m", "pm", "key", 0, "USD", Now);
        var noModule = () => InvoicePaymentClaim.Open(7, 3, " ", "pm", "key", 1000, "USD", Now);

        longKey.Should().Throw<ArgumentOutOfRangeException>();
        noAmount.Should().Throw<ArgumentOutOfRangeException>();
        noModule.Should().Throw<ArgumentException>();
    }

    /// <summary>Settling ends the claim, keeps the transaction and stamps the moment.</summary>
    [Fact]
    public void MarkSettled_EndsTheClaimWithTheTransaction()
    {
        var claim = NewClaim();

        claim.MarkSettled("txn-1", Now.AddMinutes(1));

        claim.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        claim.IsUnresolved.Should().BeFalse();
        claim.TransactionId.Should().Be("txn-1");
        claim.ResolvedAt.Should().Be(Now.AddMinutes(1));
    }

    /// <summary>Settling needs a transaction reference.</summary>
    [Fact]
    public void MarkSettled_WithoutATransaction_Throws()
    {
        var act = () => NewClaim().MarkSettled(" ", Now);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Action required and pending keep the claim unresolved, and the action address is kept only while it applies.</summary>
    [Fact]
    public void ActionRequiredAndPending_KeepTheClaimUnresolved()
    {
        var claim = NewClaim();

        claim.MarkActionRequired("https://bank.example/3ds", Now);
        claim.Status.Should().Be(InvoicePaymentClaimStatus.ActionRequired);
        claim.ActionUrl.Should().Be("https://bank.example/3ds");
        claim.IsUnresolved.Should().BeTrue();

        claim.MarkPending();
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Pending);
        claim.ActionUrl.Should().BeNull();
        claim.IsUnresolved.Should().BeTrue();
    }

    /// <summary>Declined, abandoned and held are final, and a held claim may carry no transaction.</summary>
    /// <param name="status">The final status to reach.</param>
    [Theory]
    [InlineData(InvoicePaymentClaimStatus.Declined)]
    [InlineData(InvoicePaymentClaimStatus.Abandoned)]
    [InlineData(InvoicePaymentClaimStatus.Held)]
    public void FinalStatuses_EndTheClaim(InvoicePaymentClaimStatus status)
    {
        var claim = NewClaim();

        switch (status)
        {
            case InvoicePaymentClaimStatus.Declined:
                claim.MarkDeclined(Now);
                break;
            case InvoicePaymentClaimStatus.Abandoned:
                claim.MarkAbandoned(Now);
                break;
            default:
                claim.MarkHeld(null, Now);
                break;
        }

        claim.Status.Should().Be(status);
        claim.IsUnresolved.Should().BeFalse();
        claim.ResolvedAt.Should().Be(Now);
    }

    /// <summary>A claim that ended cannot be changed again.</summary>
    [Fact]
    public void AnEndedClaim_CannotChange()
    {
        var claim = NewClaim();
        claim.MarkDeclined(Now);

        var settle = () => claim.MarkSettled("txn", Now);
        var pending = () => claim.MarkPending();
        var action = () => claim.MarkActionRequired(null, Now);

        settle.Should().Throw<InvalidOperationException>();
        pending.Should().Throw<InvalidOperationException>();
        action.Should().Throw<InvalidOperationException>();
    }

    /// <summary>A claim is stale only once the wait has passed since it was written or last looked at, and never after it ended.</summary>
    [Fact]
    public void IsStale_OnlyAfterTheWaitAndOnlyWhileUnresolved()
    {
        var claim = NewClaim();

        claim.IsStale(Now.Add(InvoicePaymentClaim.StaleAfter).AddSeconds(-1)).Should().BeFalse();
        claim.IsStale(Now.Add(InvoicePaymentClaim.StaleAfter)).Should().BeTrue();

        claim.RecordCheck(Now.AddMinutes(20));
        claim.IsStale(Now.AddMinutes(20).Add(InvoicePaymentClaim.StaleAfter).AddSeconds(-1)).Should().BeFalse();
        claim.IsStale(Now.AddMinutes(20).Add(InvoicePaymentClaim.StaleAfter)).Should().BeTrue();

        claim.MarkDeclined(Now.AddHours(2));
        claim.IsStale(Now.AddDays(3)).Should().BeFalse();
    }

    /// <summary>An action address longer than the column is cut to its width.</summary>
    [Fact]
    public void MarkActionRequired_CutsALongAddress()
    {
        var claim = NewClaim();

        claim.MarkActionRequired(new string('a', InvoicePaymentClaim.MaxActionUrlLength + 50), Now);

        claim.ActionUrl!.Length.Should().Be(InvoicePaymentClaim.MaxActionUrlLength);
    }

    /// <summary>A decline raises one declined event naming the claim, the invoice and the client.</summary>
    [Fact]
    public void MarkDeclined_RaisesTheDeclinedEvent()
    {
        var claim = NewClaim();

        claim.MarkDeclined(Now);

        var raised = claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StoredChargeDeclinedEvent>().Subject;
        raised.InvoiceId.Should().Be(claim.InvoiceId);
        raised.ClientId.Should().Be(claim.ClientId);
        raised.OccurredAt.Should().Be(Now);
    }

    /// <summary>The first action required raises one event with the address; a repeat of the status raises none.</summary>
    [Fact]
    public void MarkActionRequired_RaisesTheEventOnlyOnTheFirstReport()
    {
        var claim = NewClaim();

        claim.MarkActionRequired("https://bank.example/3ds", Now);
        claim.MarkActionRequired("https://bank.example/3ds", Now.AddMinutes(1));

        var raised = claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StoredChargeActionRequiredEvent>().Subject;
        raised.ActionUrl.Should().Be("https://bank.example/3ds");
        raised.OccurredAt.Should().Be(Now);
    }

    /// <summary>Settling, abandoning, holding or pending a claim raises neither charge event.</summary>
    [Fact]
    public void OtherEndings_RaiseNoChargeEvent()
    {
        var settled = NewClaim();
        settled.MarkSettled("txn", Now);
        var pending = NewClaim();
        pending.MarkPending();

        settled.DomainEvents.Should().BeEmpty();
        pending.DomainEvents.Should().BeEmpty();
    }

    /// <summary>A held claim has ended but still blocks a new charge; an unresolved one blocks too; a settled, declined or abandoned one does not.</summary>
    [Fact]
    public void IsBlocking_CoversUnresolvedAndHeldOnly()
    {
        var open = NewClaim();
        var held = NewClaim();
        held.MarkHeld("tx-1", Now);
        var declined = NewClaim();
        declined.MarkDeclined(Now);
        var abandoned = NewClaim();
        abandoned.MarkAbandoned(Now);
        var settled = NewClaim();
        settled.MarkSettled("tx-2", Now);

        open.IsBlocking.Should().BeTrue();
        held.IsUnresolved.Should().BeFalse();
        held.IsBlocking.Should().BeTrue("the money is taken and unsettled, so the invoice is never charged again");
        declined.IsBlocking.Should().BeFalse();
        abandoned.IsBlocking.Should().BeFalse();
        settled.IsBlocking.Should().BeFalse();
    }

    /// <summary>
    /// A declined claim and an abandoned claim the reconciliation pass checked count as failed attempts; an abandoned
    /// claim that was never checked (ended before the plugin was called) and every other status do not.
    /// </summary>
    [Fact]
    public void FailedAttemptFilter_CountsDeclinedAndCheckedAbandonedOnly()
    {
        var filter = InvoicePaymentClaim.FailedAttemptFilter.Compile();
        var declined = NewClaim();
        declined.MarkDeclined(Now);
        var checkedAbandoned = NewClaim();
        checkedAbandoned.RecordCheck(Now);
        checkedAbandoned.MarkAbandoned(Now);
        var neverSent = NewClaim();
        neverSent.MarkAbandoned(Now);
        var held = NewClaim();
        held.MarkHeld(null, Now);

        filter(declined).Should().BeTrue();
        filter(checkedAbandoned).Should().BeTrue();
        filter(neverSent).Should().BeFalse();
        filter(held).Should().BeFalse();
        filter(NewClaim()).Should().BeFalse();
    }

    /// <summary>The query filter agrees with the predicate for every status.</summary>
    [Fact]
    public void BlockingFilter_AgreesWithIsBlocking()
    {
        var filter = InvoicePaymentClaim.BlockingFilter.Compile();
        var held = NewClaim();
        held.MarkHeld(null, Now);

        filter(NewClaim()).Should().BeTrue();
        filter(held).Should().BeTrue();
        var declined = NewClaim();
        declined.MarkDeclined(Now);
        filter(declined).Should().BeFalse();
    }
}
