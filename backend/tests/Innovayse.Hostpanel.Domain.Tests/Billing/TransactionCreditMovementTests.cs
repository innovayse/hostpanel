using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for the credit history a <see cref="Transaction"/> carries: a credit-only entry that moves no cash, the
/// stamping of an entry that already holds the money, the immutability of both, and the client announcement.
/// </summary>
public sealed class TransactionCreditMovementTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A credit-only entry moves no money in or out, so every cash view and total ignores it.</summary>
    [Fact]
    public void CreateCreditMovement_MovesNoCash()
    {
        var entry = Transaction.CreateCreditMovement(
            3, Now, "Credit applied to Invoice #9", 9, "eur", CreditMovementType.InvoiceApplied, -4m, 6m);

        entry.MovesMoney.Should().BeFalse();
        entry.AmountIn.Should().Be(0m);
        entry.AmountOut.Should().Be(0m);
        entry.Fees.Should().Be(0m);
        entry.AddedToCredit.Should().BeFalse();
        entry.Currency.Should().Be("EUR");
        entry.InvoiceId.Should().Be(9);
        entry.PaymentMethod.Should().Be(PaymentMethodNames.CreditBalance);
        entry.TransactionId.Should().StartWith("credit-");
        entry.Source.Should().Be(TransactionSource.Manual);
    }

    /// <summary>The entry records why the balance moved, by how much and what it was left at.</summary>
    [Fact]
    public void CreateCreditMovement_RecordsTypeChangeAndBalance()
    {
        var entry = Transaction.CreateCreditMovement(
            3, Now, "Credit applied", 9, "USD", CreditMovementType.InvoiceApplied, -4m, 6m);

        entry.CreditMovement.Should().Be(CreditMovementType.InvoiceApplied);
        entry.CreditChange.Should().Be(-4m);
        entry.BalanceAfter.Should().Be(6m);
    }

    /// <summary>Two credit-only entries never share a reference, so the indexed column stays distinguishing.</summary>
    [Fact]
    public void CreateCreditMovement_GivesEachEntryItsOwnReference()
    {
        var first = Transaction.CreateCreditMovement(3, Now, "a", null, "USD", CreditMovementType.AdminAdd, 1m, 1m);
        var second = Transaction.CreateCreditMovement(3, Now, "b", null, "USD", CreditMovementType.AdminAdd, 1m, 2m);

        first.TransactionId.Should().NotBe(second.TransactionId);
    }

    /// <summary>A movement of nothing, or one that leaves the balance negative, is refused.</summary>
    /// <param name="change">The signed change.</param>
    /// <param name="balanceAfter">The balance it would leave.</param>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, -1)]
    public void CreateCreditMovement_RefusesAnImpossibleMovement(int change, int balanceAfter)
    {
        var act = () => Transaction.CreateCreditMovement(
            3, Now, "x", null, "USD", CreditMovementType.AdminAdd, change, balanceAfter);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>A movement that is not a defined value is refused.</summary>
    [Fact]
    public void CreateCreditMovement_RefusesAnUndefinedMovement()
    {
        var act = () => Transaction.CreateCreditMovement(3, Now, "x", null, "USD", (CreditMovementType)999, 1m, 1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>A currency that is not a three-letter code is refused, so no entry is unit-less.</summary>
    [Fact]
    public void CreateCreditMovement_RefusesAMalformedCurrency()
    {
        var act = () => Transaction.CreateCreditMovement(3, Now, "x", null, "US", CreditMovementType.AdminAdd, 1m, 1m);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>An entry that already holds the money is stamped with the movement and keeps its cash amounts.</summary>
    [Fact]
    public void RecordCreditMovement_StampsAnEntryThatHoldsCash()
    {
        var entry = Transaction.Create(3, Now, "Top-up", "tx-1", 9, "manual", 25m, 0m, 0m, false, "USD", TransactionSource.Manual);

        entry.RecordCreditMovement(CreditMovementType.TopUp, 25m, 30m);

        entry.MovesMoney.Should().BeTrue();
        entry.AmountIn.Should().Be(25m);
        entry.CreditMovement.Should().Be(CreditMovementType.TopUp);
        entry.CreditChange.Should().Be(25m);
        entry.BalanceAfter.Should().Be(30m);
    }

    /// <summary>An entry older than the history carries no movement at all, and is not invented one.</summary>
    [Fact]
    public void Create_LeavesTheCreditHistoryEmpty()
    {
        var entry = Transaction.Create(3, Now, "Top-up", "tx-1", null, "manual", 25m, 0m, 0m, true, "USD", TransactionSource.Manual);

        entry.CreditMovement.Should().BeNull();
        entry.CreditChange.Should().BeNull();
        entry.BalanceAfter.Should().BeNull();
    }

    /// <summary>An entry records one movement, never two: the second stamp is refused.</summary>
    [Fact]
    public void RecordCreditMovement_RefusesASecondStamp()
    {
        var entry = Transaction.CreateCreditMovement(3, Now, "x", null, "USD", CreditMovementType.AdminAdd, 1m, 1m);

        var act = () => entry.RecordCreditMovement(CreditMovementType.AdminDeduct, -1m, 0m);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>A movement the client is told about raises the announcement with the signed change and the new balance.</summary>
    [Fact]
    public void RecordCreditMovement_ForAnAnnouncedMovement_RaisesTheEvent()
    {
        var entry = Transaction.CreateCreditMovement(3, Now, "x", null, "usd", CreditMovementType.AdminAdd, 12m, 20m);

        var raised = entry.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CreditBalanceChangedEvent>().Subject;
        raised.ClientId.Should().Be(3);
        raised.Movement.Should().Be(CreditMovementType.AdminAdd);
        raised.Change.Should().Be(12m);
        raised.BalanceAfter.Should().Be(20m);
        raised.Currency.Should().Be("USD");
        raised.OccurredAt.Should().Be(Now);
    }

    /// <summary>Moving credit between the balance and an invoice is the client's own act or the sweep's: no announcement.</summary>
    /// <param name="movement">A movement that is not announced.</param>
    [Theory]
    [InlineData(CreditMovementType.InvoiceApplied)]
    [InlineData(CreditMovementType.InvoiceReturned)]
    [InlineData(CreditMovementType.RenewalAutoApplied)]
    [InlineData(CreditMovementType.TransactionReversed)]
    public void RecordCreditMovement_ForAQuietMovement_RaisesNoEvent(CreditMovementType movement)
    {
        var entry = Transaction.CreateCreditMovement(3, Now, "x", null, "USD", movement, 1m, 1m);

        entry.DomainEvents.Should().BeEmpty();
    }

    /// <summary>An entry of the credit history cannot be corrected, in its details or its amounts: the history is a record.</summary>
    [Fact]
    public void AnEntryOfTheHistory_RefusesCorrections()
    {
        var entry = Transaction.Create(3, Now, "Top-up", "tx-1", null, "manual", 25m, 0m, 0m, false, "USD", TransactionSource.Manual);
        entry.RecordCreditMovement(CreditMovementType.AdminAdd, 25m, 25m);

        var details = () => entry.CorrectDetails(Now, "other", "tx-2", "card");
        var amounts = () => entry.CorrectAmounts(30m, 0m, 0m);
        var removal = () => entry.EnsureEditable();

        details.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.TransactionCreditLocked);
        amounts.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.TransactionCreditLocked);
        removal.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.TransactionCreditLocked);
    }

    /// <summary>An entry that never moved the balance stays correctable and deletable as before.</summary>
    [Fact]
    public void AnEntryOutsideTheHistory_StaysEditable()
    {
        var entry = Transaction.Create(3, Now, "Top-up", "tx-1", null, "manual", 25m, 0m, 0m, true, "USD", TransactionSource.Manual);

        entry.CorrectDetails(Now, "other", "tx-2", "card");
        entry.CorrectAmounts(30m, 0m, 0m);
        entry.EnsureEditable();

        entry.Description.Should().Be("other");
        entry.AmountIn.Should().Be(30m);
    }

    /// <summary>The cash filter keeps an entry that moved money and drops a credit-only one.</summary>
    [Fact]
    public void MovedMoneyFilter_KeepsCashAndDropsCreditOnlyEntries()
    {
        var cash = Transaction.Create(3, Now, "Top-up", "tx-1", null, "manual", 25m, 0m, 0m, false, "USD", TransactionSource.Manual);
        var creditOnly = Transaction.CreateCreditMovement(3, Now, "x", null, "USD", CreditMovementType.InvoiceApplied, -1m, 0m);

        var kept = new[] { cash, creditOnly }.Where(Transaction.MovedMoneyFilter.Compile()).ToList();

        kept.Should().ContainSingle().Which.Should().BeSameAs(cash);
    }
}
