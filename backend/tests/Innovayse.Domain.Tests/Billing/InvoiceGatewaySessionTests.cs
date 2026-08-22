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
}
