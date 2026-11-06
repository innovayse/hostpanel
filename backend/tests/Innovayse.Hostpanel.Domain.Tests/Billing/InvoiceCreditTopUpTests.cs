using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for the credit top-up invoice: one fixed line, no tax, no credit, nothing to fulfil, and the amount it puts on the
/// balance. The crediting itself happens in the handlers' save; these pin what the entity allows and refuses.
/// </summary>
public sealed class InvoiceCreditTopUpTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the unpaid top-up invoice these tests act on.</summary>
    /// <param name="amount">The amount to add.</param>
    /// <param name="currency">The currency code.</param>
    /// <returns>The invoice.</returns>
    private static Invoice TopUp(decimal amount = 50m, string currency = "USD") =>
        Invoice.CreateCreditTopUp(clientId: 4, amount, currency, Now, Now.AddDays(30));

    /// <summary>A top-up is an unpaid invoice with one line for the amount, no tax and no credit, flagged as a top-up.</summary>
    [Fact]
    public void CreateCreditTopUp_IsOneUntaxedLineFlaggedAsATopUp()
    {
        var invoice = TopUp(50m);

        invoice.IsCreditTopUp.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.Items.Should().ContainSingle();
        invoice.Items[0].Description.Should().Be(Invoice.TopUpDescription);
        invoice.Items[0].ClientServiceId.Should().BeNull();
        invoice.Items[0].ProductSnapshot.Should().BeNull();
        invoice.SubTotal.Should().Be(50m);
        invoice.Tax.Should().Be(0m);
        invoice.Credit.Should().Be(0m);
        invoice.Total.Should().Be(50m);
        invoice.TopUpAmount.Should().Be(50m);
        invoice.HasDigitalItems.Should().BeFalse();
        invoice.ClientId.Should().Be(4);
    }

    /// <summary>The amount is held to the minor unit of the currency it is billed in.</summary>
    [Fact]
    public void CreateCreditTopUp_RoundsToTheCurrencyMinorUnit()
    {
        var invoice = TopUp(10.006m, "USD");

        invoice.Total.Should().Be(10.01m);
    }

    /// <summary>An amount of nothing is refused.</summary>
    /// <param name="amount">Zero or negative.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateCreditTopUp_RefusesANonPositiveAmount(int amount)
    {
        var act = () => TopUp(amount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>An ordinary invoice is not a top-up and puts nothing on the balance.</summary>
    [Fact]
    public void AnOrdinaryInvoice_IsNotATopUp()
    {
        var invoice = Invoice.Create(4, Now.AddDays(7), "USD", Now);
        invoice.AddItem("Hosting", 20m, 1);

        invoice.IsCreditTopUp.Should().BeFalse();
        invoice.TopUpAmount.Should().Be(0m);
    }

    /// <summary>The line, the quantity and the tax rate are fixed: a top-up cannot be edited into another invoice.</summary>
    [Fact]
    public void ATopUp_RefusesEveryEditOfItsLines()
    {
        var invoice = TopUp();
        var itemId = invoice.Items[0].Id;

        var add = () => invoice.AddItem("Other", 10m, 1);
        var update = () => invoice.UpdateItem(itemId, "Other", 10m, 1);
        var remove = () => invoice.RemoveItem(itemId);
        var tax = () => invoice.UpdateOptions(Now, Now.AddDays(30), null, 20m);
        var imported = () => invoice.ImportTotals(5m, 0m);

        foreach (var act in new[] { add, update, remove, tax, imported })
        {
            act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTopUpFixed);
        }

        invoice.Total.Should().Be(50m);
    }

    /// <summary>Dates and the preferred method stay editable: they are bookkeeping, not the line.</summary>
    [Fact]
    public void ATopUp_StillAcceptsNewDatesAndMethod()
    {
        var invoice = TopUp();

        invoice.UpdateOptions(Now.AddDays(1), Now.AddDays(40), "Bank Transfer", 0m);

        invoice.DueDate.Should().Be(Now.AddDays(40));
        invoice.PaymentMethod.Should().Be("Bank Transfer");
        invoice.Total.Should().Be(50m);
    }

    /// <summary>Credit cannot pay for credit: applying it is refused and the invoice still owes everything.</summary>
    [Fact]
    public void ATopUp_RefusesCredit()
    {
        var invoice = TopUp();

        var act = () => invoice.ApplyCredit(10m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTopUpNoCredit);
        invoice.Credit.Should().Be(0m);
        invoice.AmountDue.Should().Be(50m);
    }

    /// <summary>A copy would be an ordinary invoice for "account credit" that takes money and credits nothing, so it is refused.</summary>
    [Fact]
    public void ATopUp_CannotBeDuplicated()
    {
        var act = () => TopUp().Duplicate(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTopUpFixed);
    }

    /// <summary>Paying a top-up settles it like any invoice: Paid, a payment row, and the one payment event.</summary>
    [Fact]
    public void ATopUp_IsSettledLikeAnyInvoice()
    {
        var invoice = TopUp(50m);

        var ledger = invoice.Settle("Bank Transfer", "INV-1", Now, TransactionSource.Manual);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.TopUpAmount.Should().Be(50m);
        ledger!.AmountIn.Should().Be(50m);
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().ContainSingle();
    }

    /// <summary>A top-up that nobody paid can be cancelled, and nothing was ever credited for it.</summary>
    [Fact]
    public void AnUnpaidTopUp_CanBeCancelled()
    {
        var invoice = TopUp();

        invoice.Cancel();

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>A paid top-up cannot be cancelled: its money is undone by a refund.</summary>
    [Fact]
    public void APaidTopUp_CannotBeCancelled()
    {
        var invoice = TopUp();
        invoice.Settle("Bank Transfer", "INV-1", Now, TransactionSource.Manual);

        var act = () => invoice.Cancel();

        act.Should().Throw<DomainRuleException>();
    }
}
