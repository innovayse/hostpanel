using System.Globalization;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for how an <see cref="Invoice"/> guards its refunds: the claim a gateway refund holds,
/// the row change every refund makes, the cap per gateway, and the line shares an automatic refund
/// gives back.
/// </summary>
public sealed class InvoiceRefundTests
{
    /// <summary>The hosted gateway module the gateway-paid invoices in these tests were paid through.</summary>
    private const string HostedGateway = "bank-gateway";

    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a USD invoice of one line at <paramref name="price"/> and marks it paid with no payment row.</summary>
    /// <param name="price">The line price.</param>
    /// <returns>The Paid invoice.</returns>
    private static Invoice PaidFor(decimal price)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: price, quantity: 1);
        invoice.RecordPaidHistory("ref", Now);
        return invoice;
    }

    /// <summary>A partial refund changes the invoice row itself, not only its refund rows, so a racing refund fails the row's concurrency check.</summary>
    [Fact]
    public void AddRefund_Partial_StampsRefundStartedAt()
    {
        var invoice = PaidFor(100m);

        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 60m, 0m, null);

        invoice.RefundStartedAt.Should().Be(Now);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.IsRefundInFlight(Now).Should().BeFalse();
    }

    /// <summary>Two refunds of 60 on a 100 invoice cannot both be recorded.</summary>
    [Fact]
    public void AddRefund_SecondPartialAboveWhatIsLeft_ThrowsRefundExceedsPaid()
    {
        var invoice = PaidFor(100m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 60m, 0m, null);

        var act = () => invoice.AddRefund(Now.AddSeconds(1), PaymentMethodNames.Manual, "r2", 60m, 0m, null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundExceedsPaid);
    }

    /// <summary>
    /// Two partial refunds in a row keep the invoice PartiallyRefunded, each held to what was paid
    /// less the refunds before it, and the refund that reaches the whole amount paid turns it Refunded.
    /// </summary>
    [Fact]
    public void AddRefund_TwoPartialsThenTheRest_EndsRefunded()
    {
        var invoice = PaidFor(100m);

        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 30m, 0m, null);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.RefundableAmount.Should().Be(70m);

        invoice.AddRefund(Now.AddSeconds(1), PaymentMethodNames.Manual, "r2", 45m, 0m, null);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.RefundableAmount.Should().Be(25m);

        invoice.AddRefund(Now.AddSeconds(2), PaymentMethodNames.Manual, "r3", 25m, 0m, null);
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.RefundableAmount.Should().Be(0m);
        invoice.AmountRefunded.Should().Be(100m);
    }

    /// <summary>
    /// The cap after a partial refund is what was paid less that refund: a cent more is refused and
    /// leaves the invoice as it was, the exact rest is accepted.
    /// </summary>
    [Fact]
    public void AddRefund_AfterAPartial_IsCappedAtPaidLessEarlierRefunds()
    {
        var invoice = PaidFor(50m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 20m, 0m, null);

        var tooMuch = () => invoice.AddRefund(Now.AddSeconds(1), PaymentMethodNames.Manual, "r2", 30.01m, 0m, null);

        tooMuch.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundExceedsPaid);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.AmountRefunded.Should().Be(20m);

        invoice.AddRefund(Now.AddSeconds(2), PaymentMethodNames.Manual, "r3", 30m, 0m, null);
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>A single refund of the whole amount paid turns the invoice Refunded at once, never PartiallyRefunded.</summary>
    [Fact]
    public void AddRefund_OfTheWholeAmount_TurnsRefunded()
    {
        var invoice = PaidFor(80m);

        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 80m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>
    /// A PartiallyRefunded invoice is still paid: it settles nothing more, owes nothing, counts as paid in
    /// full for its renewals, can be claimed for the next refund, and cannot be reopened as Unpaid.
    /// </summary>
    [Fact]
    public void PartiallyRefunded_IsStillPaidAndSettled()
    {
        var invoice = PaidFor(100m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 10m, 0m, null);

        invoice.IsPaid.Should().BeTrue();
        invoice.IsSettled.Should().BeTrue();
        invoice.IsPayable.Should().BeFalse();
        invoice.IsPaidInFull.Should().BeTrue();
        invoice.AmountDue.Should().Be(0m);
        invoice.AmountPaid.Should().Be(100m);
        invoice.CanMarkUnpaid.Should().BeFalse();
        invoice.CanCancel.Should().BeFalse();

        invoice.BeginRefund(Now.AddSeconds(1));
        invoice.RefundStartedAt.Should().Be(Now.AddSeconds(1));
    }

    /// <summary>A Refunded invoice holds nothing more: neither a further refund nor a claim is accepted.</summary>
    [Fact]
    public void Refunded_RefusesAnotherRefund()
    {
        var invoice = PaidFor(10m);
        invoice.AddRefund(Now, PaymentMethodNames.Manual, "r1", 10m, 0m, null);

        var addRefund = () => invoice.AddRefund(Now.AddSeconds(1), PaymentMethodNames.Manual, "r2", 1m, 0m, null);
        var beginRefund = () => invoice.BeginRefund(Now.AddSeconds(1));

        addRefund.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPaid);
        beginRefund.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPaid);
        invoice.RefundableAmount.Should().Be(0m);
    }

    /// <summary><see cref="Invoice.PaidStatuses"/> names exactly the two statuses that still hold paid money.</summary>
    [Fact]
    public void PaidStatuses_ArePaidAndPartiallyRefunded()
    {
        Invoice.PaidStatuses.Should().Equal(InvoiceStatus.Paid, InvoiceStatus.PartiallyRefunded);
    }

    /// <summary>A claimed refund holds the invoice: a second claim is refused with <c>INVOICE_REFUND_CONFLICT</c>.</summary>
    [Fact]
    public void BeginRefund_WhileAnotherIsInFlight_ThrowsRefundConflict()
    {
        var invoice = PaidFor(100m);
        invoice.BeginRefund(Now);

        var act = () => invoice.BeginRefund(Now.AddSeconds(5));

        invoice.IsRefundInFlight(Now.AddSeconds(5)).Should().BeTrue();
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundConflict);
    }

    /// <summary>Any other refund is refused while a claim is in flight; the claim's own parts are recorded and end it.</summary>
    [Fact]
    public void AddRefund_UnderTheClaim_RecordsAndEndsIt_WhileAnotherIsRefused()
    {
        var invoice = PaidFor(100m);
        invoice.BeginRefund(Now);

        var other = () => invoice.AddRefund(Now.AddSeconds(1), PaymentMethodNames.Manual, "r-other", 10m, 0m, null);
        other.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundConflict);

        invoice.AddRefund(Now, PaymentMethodNames.Card, "re-1", 30m, 0m, null);
        invoice.AddRefund(Now, PaymentMethodNames.Card, "re-2", 20m, 0m, null);

        invoice.AmountRefunded.Should().Be(50m);
        invoice.IsRefundInFlight(Now.AddSeconds(1)).Should().BeFalse();
    }

    /// <summary>A claim the gateway refused outright is given back, so the next refund need not wait.</summary>
    [Fact]
    public void ReleaseRefundClaim_FreesTheInvoice()
    {
        var invoice = PaidFor(100m);
        invoice.BeginRefund(Now);

        invoice.ReleaseRefundClaim(Now);

        invoice.RefundStartedAt.Should().BeNull();
        invoice.IsRefundInFlight(Now).Should().BeFalse();
        invoice.BeginRefund(Now.AddSeconds(1));
        invoice.RefundStartedAt.Should().Be(Now.AddSeconds(1));
    }

    /// <summary>A claim that outlived its lease is treated as abandoned and no longer blocks.</summary>
    [Fact]
    public void IsRefundInFlight_PastTheLease_IsFalse()
    {
        var invoice = PaidFor(100m);
        invoice.BeginRefund(Now);

        var later = Now + Invoice.RefundClaimLease;

        invoice.IsRefundInFlight(later).Should().BeFalse();
        invoice.BeginRefund(later);
        invoice.RefundStartedAt.Should().Be(later);
    }

    /// <summary>Only a Paid invoice can be claimed for a refund.</summary>
    [Fact]
    public void BeginRefund_OnUnpaidInvoice_ThrowsNotPaid()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 10m, quantity: 1);

        var act = () => invoice.BeginRefund(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPaid);
    }

    /// <summary>A gateway can give back only what was paid through it, less what it already refunded.</summary>
    [Fact]
    public void RefundableThrough_CapsAtThatGatewaysPayments()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);
        invoice.AddPayment(Now.AddMinutes(-5), PaymentMethodNames.Manual, "pay-manual", 30m, 0m, null);
        invoice.Settle(HostedGateway, "gw-ref", Now);

        invoice.RefundableThrough(HostedGateway).Should().Be(70m);
        invoice.RefundableThrough(PaymentMethodNames.Manual).Should().Be(30m);

        invoice.AddRefund(Now, HostedGateway, "gw-refund", 20m, 0m, null);

        invoice.RefundableThrough(HostedGateway).Should().Be(50m);
        invoice.RefundableAmount.Should().Be(80m);
    }

    /// <summary>A line's share of the total is rounded to the currency's minor unit, not to four places.</summary>
    /// <param name="currency">The invoice currency.</param>
    /// <param name="lineText">The line amount, and half the second line's, invariant text so it reaches the test as an exact decimal.</param>
    /// <param name="creditText">The credit applied to the invoice, invariant text.</param>
    /// <param name="expectedText">The line's paid share, invariant text.</param>
    [Theory]
    [InlineData("USD", "10", "10", "6.67")]
    [InlineData("JPY", "1000", "1000", "667")]
    [InlineData("KWD", "1", "1", "0.667")]
    public void ShareOfTotal_RoundsToTheCurrencyMinorUnit(string currency, string lineText, string creditText, string expectedText)
    {
        var line = decimal.Parse(lineText, CultureInfo.InvariantCulture);
        var credit = decimal.Parse(creditText, CultureInfo.InvariantCulture);
        var expected = decimal.Parse(expectedText, CultureInfo.InvariantCulture);
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: currency, issuedAt: Now);
        invoice.AddItem("Domain", unitPrice: line, quantity: 1);
        invoice.AddItem("Hosting", unitPrice: line * 2, quantity: 1);
        invoice.ApplyCredit(credit);

        invoice.ShareOfTotal(line).Should().Be(expected);
    }

    /// <summary>A line's credit share and paid share add up to what the line cost.</summary>
    [Fact]
    public void ShareOfCredit_IsTheLineCostLessItsPaidShare()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Domain", unitPrice: 10m, quantity: 1);
        invoice.AddItem("Hosting", unitPrice: 20m, quantity: 1);
        invoice.ApplyCredit(10m);

        invoice.ShareOfTotal(10m).Should().Be(6.67m);
        invoice.ShareOfCredit(10m).Should().Be(3.33m);
    }

    /// <summary>A wholly credit-paid invoice has no paid share: the line goes back to credit only.</summary>
    [Fact]
    public void ShareOfCredit_OnAFullyCreditPaidInvoice_IsTheWholeLine()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Domain", unitPrice: 12m, quantity: 1);
        invoice.AddItem("Hosting", unitPrice: 38m, quantity: 1);
        invoice.ApplyCredit(50m);

        invoice.ShareOfTotal(12m).Should().Be(0m);
        invoice.ShareOfCredit(12m).Should().Be(12m);
    }

    /// <summary>An invoice that used no credit has no credit share.</summary>
    [Fact]
    public void ShareOfCredit_WithoutCredit_IsZero()
    {
        var invoice = PaidFor(100m);

        invoice.ShareOfCredit(40m).Should().Be(0m);
    }
}
