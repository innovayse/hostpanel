using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="FulfilmentContextBuilder"/>: what a fulfilment plugin is handed for an invoice. The builder is
/// pure, so these cases build the invoice in memory and read the contexts back.
/// </summary>
public sealed class FulfilmentContextBuilderTests
{
    /// <summary>The moment everything on the invoice happened at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A client linked to an account in another system.</summary>
    private static readonly ExternalAccount Account = new("crm", "acc-7", null, null);

    /// <summary>Builds an unsaved invoice that is neither paid, refunded nor lapsed.</summary>
    /// <returns>The invoice.</returns>
    private static Invoice OpenInvoice() => Invoice.Create(5, Now.AddDays(1), "USD", Now);

    /// <summary>Only the lines that carry a product snapshot are handed over, with the invoice currency and the snapshot values.</summary>
    [Fact]
    public void Lines_AreTheSnapshotLinesInTheInvoiceCurrency()
    {
        var invoice = FulfilmentInvoiceSeeds.Build(5, Now);

        var lines = FulfilmentContextBuilder.Lines(invoice);

        var line = lines.Should().ContainSingle().Subject;
        line.ProductId.Should().Be(42);
        line.Sku.Should().Be(FulfilmentInvoiceSeeds.Sku);
        line.Metadata.Should().Be(FulfilmentInvoiceSeeds.Metadata);
        line.Quantity.Should().Be(2);
        line.Amount.Should().Be(20m);
        line.Currency.Should().Be(FulfilmentInvoiceSeeds.Currency);
        line.BillingCycle.Should().Be("annual");
    }

    /// <summary>The snapshot decides the line: a monthly snapshot is handed over as monthly, with no SKU when it had none.</summary>
    [Fact]
    public void Lines_NameTheCycleOfTheSnapshot()
    {
        var invoice = OpenInvoice();
        invoice.AddItem("Hosting", 3m, 1, null, new ProductSnapshot(9, BillingCycle.Monthly, true, null, null));

        var line = FulfilmentContextBuilder.Lines(invoice).Single();

        line.BillingCycle.Should().Be("monthly");
        line.Sku.Should().BeNull();
        line.Metadata.Should().BeNull();
        line.Currency.Should().Be("USD");
    }

    /// <summary>
    /// A plugin written when only monthly and annual existed keeps receiving those exact strings, and
    /// the cycles added later are handed over by their lower-cased names, never as a number or null.
    /// </summary>
    /// <param name="cycle">The cycle on the product snapshot.</param>
    /// <param name="expected">The text the plugin contract carries.</param>
    [Theory]
    [InlineData(BillingCycle.Monthly, "monthly")]
    [InlineData(BillingCycle.Annual, "annual")]
    [InlineData(BillingCycle.Quarterly, "quarterly")]
    [InlineData(BillingCycle.SemiAnnual, "semiannual")]
    [InlineData(BillingCycle.Biennial, "biennial")]
    public void Lines_EveryCycle_IsHandedOverByItsLowerCasedName(BillingCycle cycle, string expected)
    {
        var invoice = OpenInvoice();
        invoice.AddItem("Hosting", 3m, 1, null, new ProductSnapshot(9, cycle, true, null, null));

        FulfilmentContextBuilder.Lines(invoice).Single().BillingCycle.Should().Be(expected);
    }

    /// <summary>The paid context carries the invoice, the client, the account, the payment reference and time.</summary>
    [Fact]
    public void Paid_CarriesThePaymentOfTheInvoice()
    {
        var invoice = FulfilmentInvoiceSeeds.Build(5, Now);

        var context = FulfilmentContextBuilder.Paid(invoice, Account);

        context.InvoiceId.Should().Be(invoice.Id);
        context.ClientId.Should().Be(5);
        context.Account.Should().Be(Account);
        context.TransactionReference.Should().Be(FulfilmentInvoiceSeeds.PaymentReference);
        context.PaidAt.Should().Be(Now);
        context.Lines.Should().HaveCount(1);
    }

    /// <summary>An invoice that is not paid has nothing to tell a plugin about.</summary>
    [Fact]
    public void Paid_OfAnUnpaidInvoice_Throws()
    {
        var act = () => FulfilmentContextBuilder.Paid(OpenInvoice(), null);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>The refunded context carries everything refunded so far, the invoice currency and the latest refund.</summary>
    [Fact]
    public void Refunded_CarriesTheRefundInTheInvoiceCurrency()
    {
        var invoice = FulfilmentInvoiceSeeds.Build(5, Now);

        var context = FulfilmentContextBuilder.Refunded(invoice, null);

        context.Account.Should().BeNull();
        context.RefundedAmount.Should().Be(FulfilmentInvoiceSeeds.RefundedAmount);
        context.Currency.Should().Be(FulfilmentInvoiceSeeds.Currency);
        context.TransactionReference.Should().Be(FulfilmentInvoiceSeeds.RefundReference);
        context.RefundedAt.Should().Be(Now);
    }

    /// <summary>An invoice with no refund has nothing to tell a plugin about.</summary>
    [Fact]
    public void Refunded_OfAnInvoiceWithoutRefund_Throws()
    {
        var act = () => FulfilmentContextBuilder.Refunded(OpenInvoice(), null);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>The lapsed context carries the due date and the moment the invoice lapsed.</summary>
    [Fact]
    public void Lapsed_CarriesTheDueDateAndTheLapse()
    {
        var invoice = FulfilmentInvoiceSeeds.Build(5, Now);

        var context = FulfilmentContextBuilder.Lapsed(invoice, Account);

        context.DueDate.Should().Be(Now.AddDays(-1));
        context.LapsedAt.Should().Be(Now);
        context.Account.Should().Be(Account);
    }

    /// <summary>An invoice that has not lapsed has nothing to tell a plugin about.</summary>
    [Fact]
    public void Lapsed_OfAnInvoiceThatDidNotLapse_Throws()
    {
        var act = () => FulfilmentContextBuilder.Lapsed(OpenInvoice(), null);

        act.Should().Throw<InvalidOperationException>();
    }
}
