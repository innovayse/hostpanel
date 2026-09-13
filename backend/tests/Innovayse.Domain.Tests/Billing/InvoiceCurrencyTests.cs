namespace Innovayse.Domain.Tests.Billing;

using Innovayse.Domain.Billing;

/// <summary>An invoice carries its currency from creation and never changes it.</summary>
public sealed class InvoiceCurrencyTests
{
    /// <summary>The currency given at creation is what the invoice reports, upper-cased.</summary>
    [Fact]
    public void Create_StampsTheCurrency()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(7), currency: "usd");
        Assert.Equal("USD", invoice.Currency);
    }

    /// <summary>A draft carries its currency too — it is not decided later at "send".</summary>
    [Fact]
    public void CreateDraft_StampsTheCurrency()
    {
        var invoice = Invoice.CreateDraft(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(7), currency: "AMD");
        Assert.Equal("AMD", invoice.Currency);
    }

    /// <summary>The four-argument overload stamps the currency on both a draft and an unpaid invoice.</summary>
    [Theory]
    [InlineData(true, InvoiceStatus.Draft)]
    [InlineData(false, InvoiceStatus.Unpaid)]
    public void Create_WithDraftFlag_StampsTheCurrencyAndStatus(bool isDraft, InvoiceStatus expected)
    {
        var invoice = Invoice.Create(1, DateTimeOffset.UtcNow.AddDays(7), "eur", isDraft);
        Assert.Equal("EUR", invoice.Currency);
        Assert.Equal(expected, invoice.Status);
    }

    /// <summary>A duplicate bills the same lines in the same currency as the original.</summary>
    [Fact]
    public void Duplicate_KeepsTheCurrency()
    {
        var original = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(7), currency: "AMD");
        original.AddItem("Hosting", 1000m, 1);

        var copy = original.Duplicate();

        Assert.Equal("AMD", copy.Currency);
        Assert.Equal(InvoiceStatus.Draft, copy.Status);
    }

    /// <summary>An invoice with no currency is the bug this field exists to end.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Create_RejectsAMissingOrMalformedCurrency(string currency)
        => Assert.Throws<ArgumentException>(() => Invoice.Create(1, DateTimeOffset.UtcNow.AddDays(7), currency));
}
