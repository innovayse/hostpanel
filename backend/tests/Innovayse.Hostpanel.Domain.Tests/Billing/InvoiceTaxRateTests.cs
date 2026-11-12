using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="Invoice.ApplyTaxRate"/>: the rate fixed on a freshly raised invoice.</summary>
public class InvoiceTaxRateTests
{
    /// <summary>The moment every invoice here is issued.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an unpaid invoice with one line.</summary>
    /// <param name="amount">The line price.</param>
    /// <returns>The invoice.</returns>
    private static Invoice InvoiceFor(decimal amount)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting", amount, 1);
        return invoice;
    }

    /// <summary>The rate is stored and the tax and total are computed on the subtotal.</summary>
    [Fact]
    public void ApplyTaxRate_StoresTheRateAndTaxesTheSubtotal()
    {
        var invoice = InvoiceFor(100m);

        invoice.ApplyTaxRate(19m);

        invoice.TaxRate.Should().Be(19m);
        invoice.Tax.Should().Be(19m);
        invoice.Total.Should().Be(119m);
    }

    /// <summary>A zero rate leaves the invoice untaxed.</summary>
    [Fact]
    public void ApplyTaxRate_Zero_LeavesTheInvoiceUntaxed()
    {
        var invoice = InvoiceFor(100m);

        invoice.ApplyTaxRate(0m);

        invoice.Tax.Should().Be(0m);
        invoice.Total.Should().Be(100m);
    }

    /// <summary>Lines added afterwards are taxed at the fixed rate.</summary>
    [Fact]
    public void ApplyTaxRate_ThenAnotherLine_TaxesItAtTheFixedRate()
    {
        var invoice = InvoiceFor(100m);
        invoice.ApplyTaxRate(10m);

        invoice.AddItem("Domain", 50m, 1);

        invoice.Tax.Should().Be(15m);
        invoice.Total.Should().Be(165m);
    }

    /// <summary>A paid invoice keeps the rate it was issued at.</summary>
    [Fact]
    public void ApplyTaxRate_OnPaidInvoice_Throws()
    {
        var invoice = InvoiceFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.ApplyTaxRate(20m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.Total.Should().Be(100m);
    }
}
