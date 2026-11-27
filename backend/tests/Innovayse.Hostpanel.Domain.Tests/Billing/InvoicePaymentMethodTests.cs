using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for <see cref="Invoice.PaymentMethod"/> as payments fill it: a recorded payment names the method when none
/// was chosen, and never replaces one that was.
/// </summary>
public sealed class InvoicePaymentMethodTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an unpaid invoice of 100.</summary>
    /// <returns>The invoice.</returns>
    private static Invoice UnpaidInvoice()
    {
        var invoice = Invoice.Create(clientId: 7, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting", unitPrice: 100m, quantity: 1);
        return invoice;
    }

    /// <summary>An invoice with no chosen method and no payment has no method.</summary>
    [Fact]
    public void PaymentMethod_BeforeAnyPayment_IsNull()
    {
        UnpaidInvoice().PaymentMethod.Should().BeNull();
    }

    /// <summary>A confirmed bank transfer settles the invoice and names the transfer as its method.</summary>
    [Fact]
    public void ConfirmBankTransfer_WithNoChosenMethod_SetsTheBankTransfer()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);

        invoice.ConfirmBankTransfer(Now.AddDays(1));

        invoice.PaymentMethod.Should().Be(PaymentMethodNames.BankTransfer);
    }

    /// <summary>A settlement names the gateway that took the money.</summary>
    [Fact]
    public void Settle_WithNoChosenMethod_SetsTheGateway()
    {
        var invoice = UnpaidInvoice();

        invoice.Settle("Card", "ref-1", Now, TransactionSource.GatewayPayment);

        invoice.PaymentMethod.Should().Be("Card");
    }

    /// <summary>A recorded payment names its gateway as the method.</summary>
    [Fact]
    public void AddPayment_WithNoChosenMethod_SetsTheGateway()
    {
        var invoice = UnpaidInvoice();

        invoice.AddPayment(Now, "Bank", "txn-1", 30m, 0m, null, TransactionSource.Manual);

        invoice.PaymentMethod.Should().Be("Bank");
    }

    /// <summary>The first payment names the method; a later one by another gateway does not change it.</summary>
    [Fact]
    public void AddPayment_WithAMethodAlreadySet_KeepsIt()
    {
        var invoice = UnpaidInvoice();
        invoice.AddPayment(Now, "Bank", "txn-1", 30m, 0m, null, TransactionSource.Manual);

        invoice.AddPayment(Now.AddDays(1), "cash", "txn-2", 20m, 0m, null, TransactionSource.Manual);

        invoice.PaymentMethod.Should().Be("Bank");
    }

    /// <summary>A method chosen on the invoice is kept when a payment by another gateway arrives.</summary>
    [Fact]
    public void Settle_WithAChosenMethod_KeepsTheChosenMethod()
    {
        var invoice = UnpaidInvoice();
        invoice.UpdateOptions(Now, Now.AddDays(14), "Card", 0m);

        invoice.Settle("Bank", "ref-1", Now, TransactionSource.Manual);

        invoice.PaymentMethod.Should().Be("Card");
    }
}
