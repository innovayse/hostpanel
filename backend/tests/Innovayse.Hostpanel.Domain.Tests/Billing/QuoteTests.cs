using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for the <see cref="Quote"/> aggregate's stage invariants.</summary>
public sealed class QuoteTests
{
    /// <summary>The fixed moment every quote in these tests is raised at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// AddItem on a quote whose stage was moved outside the editable set throws
    /// <see cref="DomainRuleException"/> with <c>QUOTE_NOT_EDITABLE</c>.
    /// </summary>
    [Fact]
    public void AddItem_WhenStageIsDead_ThrowsWithQuoteNotEditable()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        quote.UpdateDetails(quote.Subject, QuoteStage.Dead, quote.ExpiryDate, quote.Notes);

        var act = () => quote.AddItem("SSL", 5m, 1);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.QuoteNotEditable);
    }

    /// <summary>Accept on a Dead quote throws <see cref="DomainRuleException"/> with <c>QUOTE_NOT_ACCEPTABLE</c>.</summary>
    [Fact]
    public void Accept_WhenStageIsDead_ThrowsWithQuoteNotAcceptable()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        quote.UpdateDetails(quote.Subject, QuoteStage.Dead, quote.ExpiryDate, quote.Notes);

        var act = quote.Accept;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.QuoteNotAcceptable);
    }

    /// <summary>
    /// RemoveItem with an id that names no line on the quote throws <see cref="DomainRuleException"/>
    /// with <c>QUOTE_ITEM_NOT_FOUND</c>.
    /// </summary>
    [Fact]
    public void RemoveItem_WhenIdUnknown_ThrowsWithQuoteItemNotFound()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");

        var act = () => quote.RemoveItem(999);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.QuoteItemNotFound);
    }

    /// <summary>The lines handed to an invoice keep each line's discount, so the invoice totals what the quote does.</summary>
    [Fact]
    public void GetInvoiceItemData_KeepsTheLineDiscount()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        quote.AddItem("Hosting", 100m, 2, discountPercent: 25m);

        var line = quote.GetInvoiceItemData().Should().ContainSingle().Which;
        line.UnitPrice.Should().Be(75m);
        line.Quantity.Should().Be(2);
        (line.UnitPrice * line.Quantity).Should().Be(quote.Total).And.Be(150m);
    }

    /// <summary>The currency given at creation is what the quote reports, upper-cased.</summary>
    [Fact]
    public void Create_StampsTheCurrency()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "eur");

        quote.Currency.Should().Be("EUR");
    }

    /// <summary>A quote without a three-letter currency is refused.</summary>
    /// <param name="currency">A missing or malformed code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("US")]
    public void Create_RefusesAMalformedCurrency(string currency)
    {
        var act = () => Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: currency);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A duplicate is priced in the same currency as the original.</summary>
    [Fact]
    public void Duplicate_KeepsTheCurrency()
    {
        var original = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "GBP");

        original.Duplicate().Currency.Should().Be("GBP");
    }

    /// <summary>Backdate moves the creation date of the quote and nothing else.</summary>
    [Fact]
    public void Backdate_MovesTheCreationDateOnly()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        var written = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);

        quote.Backdate(written);

        quote.CreatedAt.Should().Be(written);
        quote.Subject.Should().Be("Test");
        quote.Stage.Should().Be(QuoteStage.Draft);
    }

    /// <summary>A discount that leaves more decimals than a two decimal currency allows is rounded on the line and in the total.</summary>
    [Fact]
    public void AddItem_WithDiscountBeyondTheCurrencyDecimals_RoundsLineAndTotalToTwoDecimals()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");

        quote.AddItem("Hosting", 10m, 3, discountPercent: 33.33m);

        quote.Items[0].AmountIn("USD").Should().Be(20.00m);
        quote.Total.Should().Be(20.00m);
    }

    /// <summary>A zero decimal currency rounds the discounted line to whole units.</summary>
    [Fact]
    public void AddItem_WithDiscountInAZeroDecimalCurrency_RoundsToWholeUnits()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "JPY");

        quote.AddItem("Hosting", 1000m, 3, discountPercent: 33.33m);

        quote.Total.Should().Be(2000m);
    }

    /// <summary>Two lines of 0.004 total 0.01, because the total is rounded once after the sum like an invoice, not once per line.</summary>
    [Fact]
    public void AddItem_TwoLinesOfFractionsOfAMinorUnit_RoundsTheSumOnce()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");

        quote.AddItem("A", 0.004m, 1);
        quote.AddItem("B", 0.004m, 1);

        quote.Total.Should().Be(0.01m);
    }

    /// <summary>The total rounds half away from zero in a currency with three decimals.</summary>
    [Fact]
    public void AddItem_InAThreeDecimalCurrency_RoundsTheSumToThreeDecimals()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "KWD");

        quote.AddItem("A", 0.0004m, 1);
        quote.AddItem("B", 0.0004m, 1);

        quote.Total.Should().Be(0.001m);
    }

    /// <summary>An update that changes the values moves the total to the rounded sum of the new lines.</summary>
    [Fact]
    public void UpdateItem_WithNewValues_RecalculatesTheTotalFromTheNewLines()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        quote.AddItem("A", 10m, 3, discountPercent: 33.33m);
        quote.AddItem("B", 0.002m, 1);
        quote.Total.Should().Be(20.00m);

        quote.UpdateItem(0, "A", 5m, 2, discountPercent: 10m);

        quote.Total.Should().Be(9.00m);
    }

    /// <summary>The total stays the rounded sum of the unrounded lines after a removal.</summary>
    [Fact]
    public void RemoveItem_KeepsTheTotalEqualToTheRoundedSumOfTheRemainingLines()
    {
        var quote = Quote.Create(clientId: 1, subject: "Test", expiryDate: Now.AddDays(7), currency: "USD");
        quote.AddItem("A", 10m, 3, discountPercent: 33.33m);
        quote.AddItem("B", 0.005m, 1);
        quote.Total.Should().Be(20.01m);

        quote.RemoveItem(0);

        quote.Total.Should().Be(0.01m);
    }
}
