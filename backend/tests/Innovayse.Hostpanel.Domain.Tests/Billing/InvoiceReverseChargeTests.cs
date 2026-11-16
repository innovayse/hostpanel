using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="Invoice.ApplyReverseCharge"/>: an invoice raised under the reverse charge.</summary>
public class InvoiceReverseChargeTests
{
    /// <summary>The moment every invoice here is issued.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an unpaid invoice with one line.</summary>
    /// <param name="amount">The line price.</param>
    /// <returns>The invoice.</returns>
    private static Invoice InvoiceFor(decimal amount)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "EUR", issuedAt: Now);
        invoice.AddItem("Hosting", amount, 1);
        return invoice;
    }

    /// <summary>A new invoice is not under the reverse charge and holds no VAT number.</summary>
    [Fact]
    public void NewInvoice_IsNotUnderTheReverseCharge()
    {
        var invoice = InvoiceFor(100m);

        invoice.ReverseCharge.Should().BeFalse();
        invoice.ClientVatNumber.Should().BeNull();
    }

    /// <summary>The reverse charge zeroes the tax and keeps the client's VAT number, trimmed, on the invoice.</summary>
    [Fact]
    public void ApplyReverseCharge_ZeroesTheTaxAndRecordsTheVatNumber()
    {
        var invoice = InvoiceFor(100m);
        invoice.ApplyTaxRate(19m);

        invoice.ApplyReverseCharge("  DE123456789 ");

        invoice.ReverseCharge.Should().BeTrue();
        invoice.ClientVatNumber.Should().Be("DE123456789");
        invoice.TaxRate.Should().Be(0m);
        invoice.Tax.Should().Be(0m);
        invoice.Total.Should().Be(100m);
    }

    /// <summary>A line added afterwards is not taxed either.</summary>
    [Fact]
    public void ApplyReverseCharge_ThenAnotherLine_StaysUntaxed()
    {
        var invoice = InvoiceFor(100m);
        invoice.ApplyReverseCharge("DE123456789");

        invoice.AddItem("Domain", 50m, 1);

        invoice.Tax.Should().Be(0m);
        invoice.Total.Should().Be(150m);
    }

    /// <summary>Setting a rate above zero afterwards ends the reverse charge, so a taxed invoice never claims it.</summary>
    [Fact]
    public void ApplyTaxRate_AboveZero_EndsTheReverseCharge()
    {
        var invoice = InvoiceFor(100m);
        invoice.ApplyReverseCharge("DE123456789");

        invoice.ApplyTaxRate(19m);

        invoice.ReverseCharge.Should().BeFalse();
        invoice.ClientVatNumber.Should().BeNull();
        invoice.Total.Should().Be(119m);
    }

    /// <summary>Setting the rate to zero again keeps the reverse charge.</summary>
    [Fact]
    public void ApplyTaxRate_Zero_KeepsTheReverseCharge()
    {
        var invoice = InvoiceFor(100m);
        invoice.ApplyReverseCharge("DE123456789");

        invoice.ApplyTaxRate(0m);

        invoice.ReverseCharge.Should().BeTrue();
        invoice.ClientVatNumber.Should().Be("DE123456789");
    }

    /// <summary>A blank VAT number cannot be the proof of a reverse charge.</summary>
    /// <param name="vatNumber">The refused value.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyReverseCharge_WithABlankNumber_Throws(string vatNumber)
    {
        var invoice = InvoiceFor(100m);

        var act = () => invoice.ApplyReverseCharge(vatNumber);

        act.Should().Throw<ArgumentException>();
        invoice.ReverseCharge.Should().BeFalse();
    }

    /// <summary>A paid invoice keeps the treatment it was issued with.</summary>
    [Fact]
    public void ApplyReverseCharge_OnPaidInvoice_Throws()
    {
        var invoice = InvoiceFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.ApplyReverseCharge("DE123456789");

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.ReverseCharge.Should().BeFalse();
    }
}
