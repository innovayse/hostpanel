using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for the <see cref="TransactionSource"/> that <see cref="Invoice"/> stamps on the ledger rows it returns.</summary>
public sealed class InvoiceLedgerSourceTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A payment recorded without a source is a manual entry.</summary>
    [Fact]
    public void AddPayment_DefaultsToAManualEntry()
    {
        var invoice = UnpaidFor(10m);

        var row = invoice.AddPayment(Now, "Bank", "tx-1", 4m, 0m, null);

        row.Source.Should().Be(TransactionSource.Manual);
    }

    /// <summary>A settlement a gateway made is recorded as a gateway payment.</summary>
    [Fact]
    public void Settle_RecordsTheGivenGatewaySource()
    {
        var invoice = UnpaidFor(10m);

        var row = invoice.Settle("Card", "ch_1", Now, TransactionSource.GatewayPayment);

        row!.Source.Should().Be(TransactionSource.GatewayPayment);
    }

    /// <summary>A settlement recorded without a source is a manual entry.</summary>
    [Fact]
    public void Settle_DefaultsToAManualEntry()
    {
        var invoice = UnpaidFor(10m);

        var row = invoice.Settle(PaymentMethodNames.Manual, "bulk-1", Now);

        row!.Source.Should().Be(TransactionSource.Manual);
    }

    /// <summary>A refund a gateway issued is recorded as a gateway refund; one that moved no gateway money stays manual.</summary>
    [Fact]
    public void AddRefund_RecordsTheGivenSourceAndDefaultsToManual()
    {
        var invoice = UnpaidFor(10m);
        invoice.Settle("Card", "ch_1", Now, TransactionSource.GatewayPayment);

        var gateway = invoice.AddRefund(Now, "Card", "re_1", 3m, 0m, null, source: TransactionSource.GatewayRefund);
        var manual = invoice.AddRefund(Now, PaymentMethodNames.Manual, "re_2", 2m, 0m, null);

        gateway.Source.Should().Be(TransactionSource.GatewayRefund);
        manual.Source.Should().Be(TransactionSource.Manual);
    }

    /// <summary>Builds an unpaid invoice for one line of <paramref name="price"/> USD.</summary>
    /// <param name="price">The line price.</param>
    /// <returns>The invoice.</returns>
    private static Invoice UnpaidFor(decimal price)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: price, quantity: 1);
        return invoice;
    }
}
