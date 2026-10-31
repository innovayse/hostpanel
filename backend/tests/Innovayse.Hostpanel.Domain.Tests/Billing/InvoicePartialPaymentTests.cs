using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// An invoice that took part of its money: it stays open until the money covers
/// <see cref="Invoice.Total"/>, settles once a credit or a lower charge leaves nothing owed
/// (<see cref="Invoice.SettleIfCovered"/>), and a closed invoice owes nothing
/// (<see cref="Invoice.AmountDue"/>). The payment rules themselves are covered in <see cref="InvoiceTests"/>.
/// </summary>
public sealed class InvoicePartialPaymentTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Today = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an open invoice for 10.00 with its creation events cleared.</summary>
    /// <returns>The invoice.</returns>
    private static Invoice OpenInvoice()
    {
        var invoice = Invoice.Create(1, Today.AddDays(7), "USD", Today);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.ClearDomainEvents();
        return invoice;
    }

    /// <summary>A payment short of the total leaves the invoice Unpaid, owing the rest, with no payment event.</summary>
    [Fact]
    public void PartialPayment_KeepsTheInvoiceOpenWithoutAPaymentEvent()
    {
        var invoice = OpenInvoice();

        invoice.AddPayment(Today, "Bank", "tx1", 4m, 0m, null, TransactionSource.Manual);

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.AmountDue.Should().Be(6m);
        invoice.PaidAt.Should().BeNull();
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().BeEmpty(
            "fulfilment, renewals and the receipt mail run once, when the invoice is paid in full");
    }

    /// <summary>
    /// Credit that covers what a partial payment left settles the invoice under the payment's
    /// reference: nothing is owed, so it must not stay open, go overdue and suspend a client who has paid.
    /// </summary>
    [Fact]
    public void SettleIfCovered_AfterCreditCoversTheRest_SettlesTheInvoice()
    {
        var invoice = OpenInvoice();
        invoice.AddPayment(Today, "Bank", "tx1", 4m, 0m, null, TransactionSource.Manual);
        invoice.ApplyCredit(6m);

        var settled = invoice.SettleIfCovered(Today.AddDays(1));

        settled.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(Today.AddDays(1));
        invoice.GatewayTransactionId.Should().Be("tx1");
        invoice.IsPaidInFull.Should().BeTrue();
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().ContainSingle();
    }

    /// <summary>Removing a line so that the payments cover what is left lets the invoice settle.</summary>
    [Fact]
    public void SettleIfCovered_AfterALineIsRemoved_SettlesTheInvoice()
    {
        // Lines are not persisted here, so every id is 0 and RemoveItem takes the first one: the
        // removed line is added first on purpose.
        var invoice = Invoice.Create(1, Today.AddDays(7), "USD", Today);
        invoice.AddItem("Setup", 5m, 1);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.AddPayment(Today, "Bank", "tx1", 10m, 0m, null, TransactionSource.Manual);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid, "10 of 15 is paid");

        invoice.RemoveItem(invoice.Items[0].Id);

        invoice.SettleIfCovered(Today).Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>An invoice that still owes money is left open.</summary>
    [Fact]
    public void SettleIfCovered_WhileMoneyIsStillDue_LeavesTheInvoiceOpen()
    {
        var invoice = OpenInvoice();
        invoice.AddPayment(Today, "Bank", "tx1", 4m, 0m, null, TransactionSource.Manual);

        invoice.SettleIfCovered(Today).Should().BeFalse();

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().BeEmpty();
    }

    /// <summary>
    /// An invoice that took no payment is not settled here even when credit covers it: that is a
    /// credit-paid invoice, closed by the path that applied the credit.
    /// </summary>
    [Fact]
    public void SettleIfCovered_WithoutAPayment_LeavesTheInvoiceOpen()
    {
        var invoice = OpenInvoice();
        invoice.ApplyCredit(10m);

        invoice.SettleIfCovered(Today).Should().BeFalse();

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A Paid invoice is not settled a second time and raises no second payment event.</summary>
    [Fact]
    public void SettleIfCovered_OnAPaidInvoice_DoesNothing()
    {
        var invoice = OpenInvoice();
        invoice.AddPayment(Today, "Bank", "tx1", 10m, 0m, null, TransactionSource.Manual);
        invoice.ClearDomainEvents();

        invoice.SettleIfCovered(Today.AddDays(1)).Should().BeFalse();

        invoice.PaidAt.Should().Be(Today);
        invoice.DomainEvents.Should().BeEmpty();
    }

    /// <summary>A cancelled invoice owes nothing, whatever its lines add up to.</summary>
    [Fact]
    public void AmountDue_OnACancelledInvoice_IsZero()
    {
        var invoice = OpenInvoice();

        invoice.Cancel();

        invoice.AmountDue.Should().Be(0m);
    }

    /// <summary>
    /// An invoice closed without a payment row (imported history) owes nothing, though no payment
    /// is recorded against its total.
    /// </summary>
    [Fact]
    public void AmountDue_OnAnInvoicePaidWithoutAPaymentRow_IsZero()
    {
        var invoice = OpenInvoice();

        invoice.RecordPaidHistory("imported", Today);

        invoice.AmountDue.Should().Be(0m);
        invoice.IsPaidInFull.Should().BeTrue();
    }

    /// <summary>A Paid invoice refuses a further payment with <c>INVOICE_NOT_PAYABLE</c>, so money is never recorded twice.</summary>
    [Fact]
    public void Payment_OnAPaidInvoice_ThrowsWithInvoiceNotPayable()
    {
        var invoice = OpenInvoice();
        invoice.AddPayment(Today, "Bank", "tx1", 10m, 0m, null, TransactionSource.Manual);

        var act = () => invoice.AddPayment(Today, "Bank", "tx2", 1m, 0m, null, TransactionSource.Manual);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPayable);
        invoice.AmountPaid.Should().Be(10m);
    }
}
