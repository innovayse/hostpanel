namespace Innovayse.Domain.Tests.Billing;

using Innovayse.Domain.Billing;
using Xunit;

/// <summary>Tests for the gateway payment session fields on <see cref="Invoice"/>.</summary>
public class InvoiceGatewaySessionTests
{
    [Fact]
    public void SetGatewaySession_OnUnpaidInvoice_StoresModuleOrderIdAndTimestamp()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        var before = DateTimeOffset.UtcNow;

        invoice.SetGatewaySession("innovayse-inecobank", "32faa424-858a-4f22");

        Assert.Equal("innovayse-inecobank", invoice.GatewayModule);
        Assert.Equal("32faa424-858a-4f22", invoice.GatewayOrderId);
        Assert.NotNull(invoice.GatewayStartedAt);
        Assert.True(invoice.GatewayStartedAt >= before);
    }

    [Fact]
    public void SetGatewaySession_SecondAttempt_OverwritesPreviousSession()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "first-attempt");

        invoice.SetGatewaySession("innovayse-inecobank", "second-attempt");

        Assert.Equal("second-attempt", invoice.GatewayOrderId);
    }

    [Fact]
    public void SetGatewaySession_OnPaidInvoice_Throws()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "attempt-1");
        invoice.MarkPaid("txn-1");

        Assert.Throws<InvalidOperationException>(
            () => invoice.SetGatewaySession("innovayse-inecobank", "attempt-2"));
    }

    [Fact]
    public void MarkPaid_WithStaleGatewaySession_ClearsIt()
    {
        // Payer started an Inecobank session, abandoned it, then paid a different way
        // (e.g. Stripe, or an admin recorded a manual payment). The stale session must not
        // survive — otherwise refund/reconciliation code would later mistake it for the
        // gateway that actually took the money.
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "abandoned-session");

        invoice.MarkPaid("stripe-txn-1");

        Assert.Null(invoice.GatewayModule);
        Assert.Null(invoice.GatewayOrderId);
        Assert.Null(invoice.GatewayStartedAt);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void MarkPaidViaGateway_RetainsTheActiveSession()
    {
        // The completion path for the invoice's own live gateway session — the session fields
        // must be retained so refund/reconciliation code can identify which gateway paid.
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "gw-order-1");

        invoice.MarkPaidViaGateway("gw-order-1");

        Assert.Equal("innovayse-inecobank", invoice.GatewayModule);
        Assert.Equal("gw-order-1", invoice.GatewayOrderId);
        Assert.NotNull(invoice.GatewayStartedAt);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void MarkPaidViaGateway_OnPaidInvoice_Throws()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "gw-order-1");
        invoice.MarkPaidViaGateway("gw-order-1");

        Assert.Throws<InvalidOperationException>(() => invoice.MarkPaidViaGateway("gw-order-2"));
    }

    [Fact]
    public void MarkUnpaid_OnGatewayPaidInvoice_ClearsSessionSoReconcilerCannotFindIt()
    {
        // An admin reversing a gateway-completed payment must leave no session behind: the
        // reconciler looks back 24 hours for pending gateway sessions, and a leftover session
        // here would let it re-query the gateway and silently re-mark the invoice Paid again,
        // undoing the admin's reversal.
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.SetGatewaySession("innovayse-inecobank", "gw-order-1");
        invoice.MarkPaidViaGateway("gw-order-1");

        invoice.MarkUnpaid();

        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Null(invoice.GatewayModule);
        Assert.Null(invoice.GatewayOrderId);
        Assert.Null(invoice.GatewayStartedAt);
        Assert.Null(invoice.GatewayTransactionId);
    }
}
