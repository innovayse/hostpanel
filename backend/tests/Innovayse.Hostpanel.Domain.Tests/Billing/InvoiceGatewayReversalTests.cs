using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for the mark that tells staff once per session that a gateway reported a settled payment as reversed
/// (<see cref="Invoice.GatewayReversalNotifiedAt"/>), and for what an invoice shows of a refund through its gateway
/// (<see cref="Invoice.ShowsRefundThrough"/>), which decides whether a reversal is news.
/// </summary>
public sealed class InvoiceGatewayReversalTests
{
    /// <summary>The hosted gateway module the invoices in these tests were paid through.</summary>
    private const string HostedGateway = "bank-gateway";

    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a USD invoice of one line at 100, with a gateway session open on it.</summary>
    /// <returns>The unpaid invoice.</returns>
    private static Invoice WithSession()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 100m, quantity: 1);
        invoice.SetGatewaySession(HostedGateway, "order-1", Now);
        return invoice;
    }

    /// <summary>Creates the invoice of <see cref="WithSession"/> and settles it through the gateway.</summary>
    /// <returns>The invoice paid through <see cref="HostedGateway"/>.</returns>
    private static Invoice PaidThroughTheGateway()
    {
        var invoice = WithSession();
        invoice.Settle(HostedGateway, "tx-1", Now, TransactionSource.GatewayPayment);
        return invoice;
    }

    /// <summary>A session nobody was told about has no mark.</summary>
    [Fact]
    public void GatewayReversalNotifiedAt_IsUnsetByDefault()
    {
        WithSession().GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>Marking records the moment staff were told, and nothing else on the invoice moves.</summary>
    [Fact]
    public void MarkGatewayReversalNotified_RecordsTheMomentAndLeavesTheInvoicePaid()
    {
        var invoice = PaidThroughTheGateway();

        invoice.MarkGatewayReversalNotified(Now.AddHours(1));

        invoice.GatewayReversalNotifiedAt.Should().Be(Now.AddHours(1));
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.WasPaidThrough(HostedGateway).Should().BeTrue();
    }

    /// <summary>A new session starts clean: it is told again if its payment is reversed.</summary>
    [Fact]
    public void SetGatewaySession_ClearsTheMark()
    {
        var invoice = WithSession();
        invoice.MarkGatewayReversalNotified(Now);

        invoice.SetGatewaySession(HostedGateway, "order-2", Now.AddMinutes(5));

        invoice.GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>An invoice paid through the gateway with no refund shows none, so a reversal is news.</summary>
    [Fact]
    public void ShowsRefundThrough_PaidWithNoRefund_IsFalse()
    {
        PaidThroughTheGateway().ShowsRefundThrough(HostedGateway, Now.AddHours(1)).Should().BeFalse();
    }

    /// <summary>A refund recorded through the gateway explains a reversal.</summary>
    [Fact]
    public void ShowsRefundThrough_PartialRefundThroughTheGateway_IsTrue()
    {
        var invoice = PaidThroughTheGateway();
        invoice.AddRefund(Now.AddHours(1), HostedGateway, "refund-1", 40m, 0m, null, false, TransactionSource.GatewayRefund);

        invoice.ShowsRefundThrough(HostedGateway, Now.AddHours(2)).Should().BeTrue();
    }

    /// <summary>A refund through another gateway or by hand is not a refund through this one, while the invoice is not refunded in full.</summary>
    [Fact]
    public void ShowsRefundThrough_RefundByHand_IsFalseForTheGateway()
    {
        var invoice = PaidThroughTheGateway();
        invoice.AddRefund(Now.AddHours(1), PaymentMethodNames.Manual, "refund-1", 40m, 0m, null, false, TransactionSource.Manual);

        invoice.ShowsRefundThrough(HostedGateway, Now.AddHours(2)).Should().BeFalse();
    }

    /// <summary>A refund still waiting for its gateway explains a reversal that arrives before it is recorded.</summary>
    [Fact]
    public void ShowsRefundThrough_RefundInFlight_IsTrue()
    {
        var invoice = PaidThroughTheGateway();
        invoice.BeginGatewayRefund(Now.AddHours(1), 40m);

        invoice.ShowsRefundThrough(HostedGateway, Now.AddHours(1).AddSeconds(1)).Should().BeTrue();
    }

    /// <summary>An invoice refunded in full shows a refund whichever way the money went back.</summary>
    [Fact]
    public void ShowsRefundThrough_RefundedInFull_IsTrue()
    {
        var invoice = PaidThroughTheGateway();
        invoice.AddRefund(Now.AddHours(1), PaymentMethodNames.Manual, "refund-1", 100m, 0m, null, false, TransactionSource.Manual);

        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.ShowsRefundThrough(HostedGateway, Now.AddDays(2)).Should().BeTrue();
    }
}
