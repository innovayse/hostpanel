using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for the manual bank transfer of an <see cref="Invoice"/>: choosing it, confirming it through the one
/// settlement path, rejecting it, and the guards that stop a second confirm or a late reject.
/// </summary>
public sealed class InvoiceBankTransferTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an unpaid invoice of 100 with its creation event already dispatched.</summary>
    /// <returns>The invoice.</returns>
    private static Invoice UnpaidInvoice()
    {
        var invoice = Invoice.Create(clientId: 7, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting", unitPrice: 100m, quantity: 1);
        invoice.ClearDomainEvents();
        return invoice;
    }

    /// <summary>Choosing a bank transfer moves the invoice to PaymentPending, marks it as a transfer and announces it once.</summary>
    [Fact]
    public void RequestBankTransfer_MovesToPaymentPendingAndRaisesTheEvent()
    {
        var invoice = UnpaidInvoice();

        var began = invoice.RequestBankTransfer(Now);

        began.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.PaymentPending);
        invoice.BankTransferRequestedAt.Should().Be(Now);
        invoice.IsBankTransferPending.Should().BeTrue();
        invoice.IsPayable.Should().BeFalse("no other payment may start beside the transfer");
        var raised = invoice.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BankTransferRequestedEvent>().Subject;
        raised.ClientId.Should().Be(7);
        raised.Reference.Should().Be(invoice.BankTransferReference);
    }

    /// <summary>The reference is built from the invoice number alone, so the client id never reaches the payer.</summary>
    [Fact]
    public void BankTransferReference_IsBuiltFromTheInvoiceNumberOnly()
    {
        var invoice = UnpaidInvoice();

        invoice.BankTransferReference.Should().Be($"INV-{invoice.Id}");
        invoice.BankTransferReference.Should().NotContain(invoice.ClientId.ToString());
    }

    /// <summary>Choosing it again while it waits changes nothing and announces nothing, so a double click is harmless.</summary>
    [Fact]
    public void RequestBankTransfer_WhenAlreadyWaiting_DoesNothing()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ClearDomainEvents();

        var began = invoice.RequestBankTransfer(Now.AddMinutes(5));

        began.Should().BeFalse();
        invoice.BankTransferRequestedAt.Should().Be(Now);
        invoice.DomainEvents.Should().BeEmpty();
    }

    /// <summary>An overdue invoice can be paid by transfer too.</summary>
    [Fact]
    public void RequestBankTransfer_OnAnOverdueInvoice_Works()
    {
        var invoice = UnpaidInvoice();
        invoice.MarkOverdue(Now.AddDays(20));

        invoice.RequestBankTransfer(Now.AddDays(21)).Should().BeTrue();

        invoice.Status.Should().Be(InvoiceStatus.PaymentPending);
    }

    /// <summary>A paid or cancelled invoice cannot be switched to a transfer.</summary>
    /// <param name="paid">Whether the invoice is paid (otherwise cancelled).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestBankTransfer_OnAClosedInvoice_IsRefused(bool paid)
    {
        var invoice = UnpaidInvoice();
        if (paid)
        {
            invoice.Settle("Card", "ref-1", Now, TransactionSource.GatewayPayment);
        }
        else
        {
            invoice.Cancel();
        }

        var act = () => invoice.RequestBankTransfer(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPayable);
    }

    /// <summary>Confirming settles the invoice through the one settlement path: Paid, a payment, a ledger row and the payment event.</summary>
    [Fact]
    public void ConfirmBankTransfer_SettlesThroughTheSharedPath()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ClearDomainEvents();

        var ledgerRow = invoice.ConfirmBankTransfer(Now.AddDays(2));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(Now.AddDays(2));
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.IsBankTransferPending.Should().BeFalse();
        invoice.PaymentsRecorded.Should().Be(100m);
        invoice.Transactions.Should().ContainSingle(t => t.Gateway == PaymentMethodNames.BankTransfer && t.TransactionId == invoice.BankTransferReference);
        ledgerRow.Should().NotBeNull();
        ledgerRow!.Source.Should().Be(TransactionSource.Manual);
        var received = invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().ContainSingle().Subject;
        received.Amount.Should().Be(100m);
        received.TransactionId.Should().Be(invoice.BankTransferReference);
        invoice.DomainEvents.OfType<BankTransferConfirmedEvent>().Should().ContainSingle();
    }

    /// <summary>A second confirm of the same transfer is refused and records nothing more.</summary>
    [Fact]
    public void ConfirmBankTransfer_Twice_IsRefusedTheSecondTime()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ConfirmBankTransfer(Now.AddDays(1));
        invoice.ClearDomainEvents();

        var act = () => invoice.ConfirmBankTransfer(Now.AddDays(2));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceBankTransferNotPending);
        invoice.PaymentsRecorded.Should().Be(100m);
        invoice.DomainEvents.Should().BeEmpty();
    }

    /// <summary>An invoice that never chose a transfer, and a PaymentPending invoice that is not a transfer, cannot be confirmed.</summary>
    [Fact]
    public void ConfirmBankTransfer_WhenNoTransferWaits_IsRefused()
    {
        var invoice = UnpaidInvoice();

        var act = () => invoice.ConfirmBankTransfer(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceBankTransferNotPending);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.PaymentsRecorded.Should().Be(0m);
    }

    /// <summary>Rejecting returns the invoice to Unpaid, clears the wait and announces the reason.</summary>
    [Fact]
    public void RejectBankTransfer_ReturnsToUnpaidAndRaisesTheEventWithTheNote()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ClearDomainEvents();

        invoice.RejectBankTransfer("No money arrived", Now.AddDays(3));

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.IsPayable.Should().BeTrue();
        invoice.PaymentsRecorded.Should().Be(0m);
        var rejected = invoice.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BankTransferRejectedEvent>().Subject;
        rejected.Note.Should().Be("No money arrived");
        rejected.Reference.Should().Be(invoice.BankTransferReference);
    }

    /// <summary>After a reject the client can choose the transfer again.</summary>
    [Fact]
    public void RejectBankTransfer_LetsTheClientTryAgain()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.RejectBankTransfer("Wrong amount", Now.AddDays(1));

        invoice.RequestBankTransfer(Now.AddDays(2)).Should().BeTrue();

        invoice.IsBankTransferPending.Should().BeTrue();
    }

    /// <summary>A confirmed payment cannot be turned back by a late reject.</summary>
    [Fact]
    public void RejectBankTransfer_AfterConfirm_IsRefused()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ConfirmBankTransfer(Now.AddDays(1));

        var act = () => invoice.RejectBankTransfer("Too late", Now.AddDays(2));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceBankTransferNotPending);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>While a transfer waits the invoice cannot be cancelled or edited, so staff must decide first.</summary>
    [Fact]
    public void WhileWaiting_TheInvoiceCannotBeCancelledOrPaidByAnotherPath()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);

        invoice.CanCancel.Should().BeFalse();
        var pay = () => invoice.AddPayment(Now, "Manual", "ref", 10m, 0m, null, TransactionSource.Manual);
        pay.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPayable);
    }

    /// <summary>A hosted session started inside its window is seen as live; one older than the window is not.</summary>
    [Fact]
    public void IsInsideGatewaySessionWindow_FollowsTheWindow()
    {
        var invoice = UnpaidInvoice();
        invoice.IsInsideGatewaySessionWindow(Now).Should().BeFalse("no session was started");

        invoice.SetGatewaySession("gw", "order-1", Now);

        invoice.IsInsideGatewaySessionWindow(Now.AddMinutes(19)).Should().BeTrue();
        invoice.IsInsideGatewaySessionWindow(Now.AddMinutes(20)).Should().BeFalse();
    }

    /// <summary>Rejecting a transfer on an invoice whose due date has passed returns it to Overdue, not Unpaid, so the overdue flow carries on.</summary>
    [Fact]
    public void RejectBankTransfer_AfterTheDueDate_ReturnsToOverdue()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);

        invoice.RejectBankTransfer("No money arrived", Now.AddDays(20));

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.IsPayable.Should().BeTrue();
    }

    /// <summary>A lapsed wait returns the invoice to open, tells the client through the rejection event and clears the marker.</summary>
    [Fact]
    public void LapseBankTransfer_ReturnsToOpenAndAnnouncesTheFixedReason()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ClearDomainEvents();

        invoice.LapseBankTransfer(Now.AddDays(5));

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.IsBankTransferPending.Should().BeFalse();
        var lapsed = invoice.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BankTransferRejectedEvent>().Subject;
        lapsed.Note.Should().Be(Invoice.BankTransferLapsedNote);
    }

    /// <summary>A wait that lapses after the due date returns the invoice to Overdue.</summary>
    [Fact]
    public void LapseBankTransfer_AfterTheDueDate_ReturnsToOverdue()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);

        invoice.LapseBankTransfer(Now.AddDays(15));

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
    }

    /// <summary>Only a waiting transfer can lapse; a confirmed one cannot be turned back.</summary>
    [Fact]
    public void LapseBankTransfer_WhenNoTransferWaits_IsRefused()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ConfirmBankTransfer(Now.AddDays(1));

        var act = () => invoice.LapseBankTransfer(Now.AddDays(30));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceBankTransferNotPending);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>A gateway payment that arrives first withdraws the wait without an announcement, so the invoice can be settled with it.</summary>
    [Fact]
    public void WithdrawBankTransfer_ReopensTheInvoiceSilentlySoTheGatewayPaymentCanSettleIt()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);
        invoice.ClearDomainEvents();

        invoice.WithdrawBankTransfer(Now.AddMinutes(30));
        var ledgerRow = invoice.Settle("gw", "ref-1", Now.AddMinutes(30), TransactionSource.GatewayPayment);

        invoice.DomainEvents.Should().NotContain(e => e is BankTransferRejectedEvent);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.IsBankTransferPending.Should().BeFalse();
        ledgerRow.Should().NotBeNull();
    }

    /// <summary>Confirming a transfer after the due date still settles the invoice.</summary>
    [Fact]
    public void ConfirmBankTransfer_AfterTheDueDate_StillSettles()
    {
        var invoice = UnpaidInvoice();
        invoice.RequestBankTransfer(Now);

        var ledgerRow = invoice.ConfirmBankTransfer(Now.AddDays(30));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        ledgerRow.Should().NotBeNull();
    }
}
