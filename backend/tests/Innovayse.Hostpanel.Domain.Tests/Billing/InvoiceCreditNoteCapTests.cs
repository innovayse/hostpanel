using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for what an <see cref="Invoice"/> allows a credit note to cover: only a paid invoice that is not a top-up, never more
/// than the charge less every earlier refund and every earlier account credit note, and the way a credit note held out of the cash
/// that can still be refunded, so the same money is never given back twice.
/// </summary>
public sealed class InvoiceCreditNoteCapTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a USD invoice of one line at <paramref name="price"/> taxed at <paramref name="taxRate"/> and marks it paid.</summary>
    /// <param name="price">The line price.</param>
    /// <param name="taxRate">The tax percentage.</param>
    /// <returns>The Paid invoice.</returns>
    private static Invoice PaidFor(decimal price, decimal taxRate = 0m)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: price, quantity: 1);
        invoice.UpdateOptions(Now, Now.AddDays(7), null, taxRate);
        invoice.RecordPaidHistory("ref", Now);
        return invoice;
    }

    /// <summary>A paid invoice can be credited for its charge before credit: the sub total plus the tax.</summary>
    [Fact]
    public void CreditableAmount_OfAPaidInvoice_IsItsChargeWithTax()
    {
        var invoice = PaidFor(100m, taxRate: 20m);

        invoice.CreditableAmount.Should().Be(120m);
    }

    /// <summary>An invoice that is not paid has nothing to credit.</summary>
    [Fact]
    public void CreditableAmount_OfAnUnpaidInvoice_IsZero()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);

        invoice.CreditableAmount.Should().Be(0m);
    }

    /// <summary>A credit top-up is credit already: it can never take a credit note, and says why.</summary>
    [Fact]
    public void EnsureCreditable_OnAPaidTopUp_IsRefused()
    {
        var topUp = Invoice.CreateCreditTopUp(clientId: 1, 50m, "USD", Now, Now.AddDays(7));
        topUp.RecordPaidHistory("ref", Now);

        var act = () => topUp.EnsureCreditable(10m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteTopUpInvoice);
        topUp.CreditableAmount.Should().Be(0m);
    }

    /// <summary>An unpaid invoice is refused with the not-paid code.</summary>
    [Fact]
    public void EnsureCreditable_OnAnUnpaidInvoice_IsRefused()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);

        var act = () => invoice.EnsureCreditable(10m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPaid);
    }

    /// <summary>An earlier refund is counted: what was refunded cannot be credited again.</summary>
    [Fact]
    public void CreditableAmount_CountsEarlierRefunds()
    {
        var invoice = PaidFor(100m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 30m, 0m, null, false, TransactionSource.Manual);

        invoice.CreditableAmount.Should().Be(70m);
        var act = () => invoice.EnsureCreditable(70.01m);
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteExceedsCreditable);
    }

    /// <summary>An earlier account credit note is counted, and a second one is held to what is left.</summary>
    [Fact]
    public void AddCreditNoted_IsHeldToWhatEarlierNotesLeft()
    {
        var invoice = PaidFor(100m);
        invoice.AddCreditNoted(60m);

        invoice.CreditNotedAmount.Should().Be(60m);
        invoice.CreditableAmount.Should().Be(40m);
        var act = () => invoice.AddCreditNoted(40.01m);
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteExceedsCreditable);
        invoice.CreditNotedAmount.Should().Be(60m);
    }

    /// <summary>Credit already given back to the balance is held out of the cash that can still be refunded.</summary>
    [Fact]
    public void AddCreditNoted_LowersTheRefundableAmount()
    {
        var invoice = PaidFor(100m);

        invoice.AddCreditNoted(60m);

        invoice.RefundableAmount.Should().Be(40m);
        var act = () => invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 40.01m, 0m, null, false, TransactionSource.Manual);
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundExceedsPaid);
    }

    /// <summary>A refund and a credit note together can never pass the charge, in either order.</summary>
    [Fact]
    public void RefundAndCreditNote_TogetherCannotPassTheCharge()
    {
        var invoice = PaidFor(100m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 70m, 0m, null, false, TransactionSource.Manual);

        var tooMuch = () => invoice.AddCreditNoted(30.01m);
        tooMuch.Should().Throw<DomainRuleException>();
        invoice.AddCreditNoted(30m);

        invoice.CreditableAmount.Should().Be(0m);
        invoice.RefundableAmount.Should().Be(0m);
    }

    /// <summary>Voiding a note releases its amount so the invoice can be credited or refunded again.</summary>
    [Fact]
    public void RemoveCreditNoted_GivesTheAmountBack()
    {
        var invoice = PaidFor(100m);
        invoice.AddCreditNoted(60m);

        invoice.RemoveCreditNoted(60m);

        invoice.CreditNotedAmount.Should().Be(0m);
        invoice.CreditableAmount.Should().Be(100m);
        invoice.RefundableAmount.Should().Be(100m);
    }

    /// <summary>Releasing more than was noted is refused rather than driving the amount negative.</summary>
    [Fact]
    public void RemoveCreditNoted_MoreThanWasNoted_IsRefused()
    {
        var invoice = PaidFor(100m);
        invoice.AddCreditNoted(10m);

        var act = () => invoice.RemoveCreditNoted(10.01m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteInvalidState);
    }

    /// <summary>A note of nothing is refused.</summary>
    /// <param name="amount">Zero or negative.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddCreditNoted_ANonPositiveAmount_IsRefused(int amount)
    {
        var invoice = PaidFor(100m);

        var act = () => invoice.AddCreditNoted(amount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
