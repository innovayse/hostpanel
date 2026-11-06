using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for <see cref="CreditNote"/>: totals at the invoice's own tax rate, the caps it is built against, the numbering and
/// the lifecycle from Draft to Applied or Voided, including the rule that a note paid out as a gateway refund is never voided.
/// </summary>
public sealed class CreditNoteTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a USD invoice of one 100.00 line taxed at <paramref name="taxRate"/>, marked paid.</summary>
    /// <param name="taxRate">The tax percentage.</param>
    /// <returns>The Paid invoice.</returns>
    private static Invoice PaidInvoice(decimal taxRate = 20m)
    {
        var invoice = Invoice.Create(clientId: 7, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);
        invoice.UpdateOptions(Now, Now.AddDays(7), null, taxRate);
        invoice.RecordPaidHistory("ref", Now);
        return invoice;
    }

    /// <summary>Builds one credit note line in USD.</summary>
    /// <param name="price">The unit price.</param>
    /// <param name="quantity">The quantity.</param>
    /// <returns>The line.</returns>
    private static CreditNoteItem Line(decimal price, int quantity = 1) =>
        CreditNoteItem.Create("Credit for Hosting Plan", price, quantity, "USD");

    /// <summary>Builds a draft account credit note of one line against the invoice.</summary>
    /// <param name="invoice">The invoice to credit.</param>
    /// <param name="price">The line price.</param>
    /// <param name="method">How the value goes back.</param>
    /// <returns>The draft.</returns>
    private static CreditNote Draft(Invoice invoice, decimal price, CreditNoteMethod method = CreditNoteMethod.AccountCredit) =>
        CreditNote.Create(invoice, [Line(price)], method, "Service was down", Now);

    /// <summary>The note copies the client, currency and tax rate of the invoice and taxes the lines at that same rate.</summary>
    [Fact]
    public void Create_FollowsTheInvoiceAtTheSameRate()
    {
        var invoice = PaidInvoice(taxRate: 20m);

        var note = Draft(invoice, 50m);

        note.ClientId.Should().Be(7);
        note.Currency.Should().Be("USD");
        note.TaxRate.Should().Be(20m);
        note.SubTotal.Should().Be(50m);
        note.Tax.Should().Be(10m);
        note.Total.Should().Be(60m);
        note.Status.Should().Be(CreditNoteStatus.Draft);
        note.Number.Should().BeNull();
    }

    /// <summary>A line total is the unit price times the quantity, rounded to the currency's minor unit.</summary>
    [Fact]
    public void CreditNoteItem_RoundsItsAmountToTheMinorUnit()
    {
        var line = CreditNoteItem.Create("Metered", 0.333m, 3, "USD");

        line.Amount.Should().Be(1.00m);
    }

    /// <summary>The whole charge of the invoice can be credited in full, and not a cent more.</summary>
    [Fact]
    public void Create_ForTheWholeCharge_IsAllowedAndAboveItIsRefused()
    {
        var invoice = PaidInvoice(taxRate: 20m);

        Draft(invoice, 100m).Total.Should().Be(120m);
        var act = () => Draft(invoice, 100.01m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteExceedsCreditable);
    }

    /// <summary>Earlier refunds and earlier account credit notes count against the cap.</summary>
    [Fact]
    public void Create_IsCappedByEarlierRefundsAndNotes()
    {
        var invoice = PaidInvoice(taxRate: 0m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 30m, 0m, null, false, TransactionSource.Manual);
        invoice.AddCreditNoted(50m);

        Draft(invoice, 20m).Total.Should().Be(20m);
        var act = () => Draft(invoice, 20.01m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteExceedsCreditable);
    }

    /// <summary>
    /// A gateway refund note is also held to the cash that can still be refunded: an invoice of 100 that 30 of invoice credit
    /// paid for took only 70 in cash, so it can be credited 100 as account credit but refunded 70 at most.
    /// </summary>
    [Fact]
    public void Create_AsAGatewayRefund_IsHeldToTheRefundableCash()
    {
        var invoice = Invoice.Create(clientId: 7, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);
        invoice.ApplyCredit(30m);
        invoice.AddPayment(Now, PaymentMethodNames.Manual, "pay-1", 70m, 0m, null, TransactionSource.Manual);

        var tooMuchCash = () => Draft(invoice, 80m, CreditNoteMethod.GatewayRefund);
        var asCredit = Draft(invoice, 80m);

        tooMuchCash.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundExceedsPaid);
        asCredit.Total.Should().Be(80m);
        Draft(invoice, 70m, CreditNoteMethod.GatewayRefund).Total.Should().Be(70m);
    }

    /// <summary>A credit note on a credit top-up is refused with its own code.</summary>
    [Fact]
    public void Create_OnATopUp_IsRefused()
    {
        var topUp = Invoice.CreateCreditTopUp(clientId: 7, 50m, "USD", Now, Now.AddDays(7));
        topUp.RecordPaidHistory("ref", Now);

        var act = () => Draft(topUp, 10m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteTopUpInvoice);
    }

    /// <summary>A note without a line, a reason or a valid line is refused.</summary>
    [Fact]
    public void Create_WithoutLinesOrReason_IsRefused()
    {
        var invoice = PaidInvoice();

        var noLines = () => CreditNote.Create(invoice, [], CreditNoteMethod.AccountCredit, "Because", Now);
        var noReason = () => CreditNote.Create(invoice, [Line(10m)], CreditNoteMethod.AccountCredit, "  ", Now);
        var badLine = () => CreditNoteItem.Create("  ", 10m, 1, "USD");
        var freeLine = () => CreditNoteItem.Create("Free", 0m, 1, "USD");
        var noUnits = () => CreditNoteItem.Create("Nothing", 5m, 0, "USD");

        noLines.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteItemsRequired);
        noReason.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteItemsRequired);
        badLine.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteItemsRequired);
        freeLine.Should().Throw<DomainRuleException>();
        noUnits.Should().Throw<DomainRuleException>();
    }

    /// <summary>The number is the prefix and at least four digits, and longer when the sequence outgrows them.</summary>
    /// <param name="value">The sequence value.</param>
    /// <param name="expected">The number printed.</param>
    [Theory]
    [InlineData(1, "CN-0001")]
    [InlineData(42, "CN-0042")]
    [InlineData(9999, "CN-9999")]
    [InlineData(12345, "CN-12345")]
    public void FormatNumber_PadsToFourDigits(long value, string expected)
    {
        CreditNote.FormatNumber(value).Should().Be(expected);
    }

    /// <summary>A sequence value of nothing is not a number.</summary>
    [Fact]
    public void FormatNumber_OfZero_IsRefused()
    {
        var act = () => CreditNote.FormatNumber(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Issuing gives the draft its number and date; applying raises the event that mails the client.</summary>
    [Fact]
    public void IssueThenApply_NumbersTheNoteAndRaisesTheMail()
    {
        var note = Draft(PaidInvoice(), 50m);

        note.Issue("CN-0001", Now);
        note.Status.Should().Be(CreditNoteStatus.Issued);
        note.Number.Should().Be("CN-0001");
        note.IssuedAt.Should().Be(Now);
        note.DomainEvents.Should().BeEmpty();

        note.MarkApplied(Now.AddSeconds(1), refundReference: null);

        note.Status.Should().Be(CreditNoteStatus.Applied);
        note.AppliedAt.Should().Be(Now.AddSeconds(1));
        var raised = note.DomainEvents.Should().ContainSingle().Subject.Should().BeOfType<CreditNoteIssuedEvent>().Subject;
        raised.Number.Should().Be("CN-0001");
        raised.Total.Should().Be(60m);
        raised.ClientId.Should().Be(7);
    }

    /// <summary>A note is numbered once, applied only after it is issued, and issued only from a draft.</summary>
    [Fact]
    public void TheLifecycle_RefusesAStepOutOfOrder()
    {
        var note = Draft(PaidInvoice(), 50m);

        var applyDraft = () => note.MarkApplied(Now, null);
        applyDraft.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteInvalidState);

        note.Issue("CN-0001", Now);
        var issueTwice = () => note.Issue("CN-0002", Now);
        issueTwice.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteInvalidState);
        note.Number.Should().Be("CN-0001");
    }

    /// <summary>A gateway refund note records the reference of the refund it paid out, and cannot be applied without one.</summary>
    [Fact]
    public void MarkApplied_OnAGatewayRefundNote_NeedsTheRefundReference()
    {
        var note = Draft(PaidInvoice(), 50m, CreditNoteMethod.GatewayRefund);
        note.Issue("CN-0001", Now);

        var without = () => note.MarkApplied(Now, null);
        without.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteInvalidState);

        note.MarkApplied(Now, "re_123");
        note.RefundReference.Should().Be("re_123");
    }

    /// <summary>Only an account credit note counts against the invoice's noted amount; a refund note is already a refund row.</summary>
    [Fact]
    public void HoldsInvoiceCredit_OnlyForAnAccountCreditNoteThatStands()
    {
        var credit = Draft(PaidInvoice(), 10m);
        var refund = Draft(PaidInvoice(), 10m, CreditNoteMethod.GatewayRefund);

        credit.HoldsInvoiceCredit.Should().BeFalse();
        credit.Issue("CN-0001", Now);
        credit.HoldsInvoiceCredit.Should().BeTrue();
        credit.MarkApplied(Now, null);
        credit.HoldsInvoiceCredit.Should().BeTrue();
        credit.Void("Mistake", Now);
        credit.HoldsInvoiceCredit.Should().BeFalse();
        refund.Issue("CN-0002", Now);
        refund.HoldsInvoiceCredit.Should().BeFalse();
    }

    /// <summary>A voided account credit note keeps its number, records when and why, and cannot be voided again.</summary>
    [Fact]
    public void Void_KeepsTheNumberAndIsOnlyPossibleOnce()
    {
        var note = Draft(PaidInvoice(), 50m);
        note.Issue("CN-0001", Now);
        note.MarkApplied(Now, null);

        note.Void("Issued to the wrong invoice", Now.AddHours(1));

        note.Status.Should().Be(CreditNoteStatus.Voided);
        note.Number.Should().Be("CN-0001");
        note.VoidedAt.Should().Be(Now.AddHours(1));
        note.VoidReason.Should().Be("Issued to the wrong invoice");
        var again = () => note.Void("Again", Now);
        again.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteAlreadyVoided);
    }

    /// <summary>A draft was never issued, so it has nothing to void.</summary>
    [Fact]
    public void Void_OfADraft_IsRefused()
    {
        var note = Draft(PaidInvoice(), 50m);

        var act = () => note.Void("Nope", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteInvalidState);
    }

    /// <summary>A note paid out as a gateway refund is never voided: the refund cannot be taken back.</summary>
    [Fact]
    public void Void_OfAPaidOutRefundNote_IsRefused()
    {
        var note = Draft(PaidInvoice(), 50m, CreditNoteMethod.GatewayRefund);
        note.Issue("CN-0001", Now);
        note.MarkApplied(Now, "re_123");

        var act = () => note.Void("Changed my mind", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.CreditNoteRefundNotVoidable);
        note.Status.Should().Be(CreditNoteStatus.Applied);
    }
}
