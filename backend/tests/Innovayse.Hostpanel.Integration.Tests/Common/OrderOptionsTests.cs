using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Orders;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// <see cref="OrderOptions"/>: how an order number is built from the sequence value, the payment
/// term it gives an order's invoice, and which values it refuses at startup.
/// </summary>
public sealed class OrderOptionsTests
{
    /// <summary>The defaults number orders the way they always have been: <c>ORD-</c> and four digits.</summary>
    [Fact]
    public void FormatNumber_Defaults_PadsToFourDigitsAfterTheOrdPrefix()
    {
        var options = new OrderOptions();

        options.FormatNumber(42).Should().Be("ORD-0042");
        options.FormatNumber(123456).Should().Be("ORD-123456");
        options.InvoicePaymentTerm.Should().Be(TimeSpan.FromDays(7));
        options.IsValid.Should().BeTrue();
    }

    /// <summary>A configured prefix and digit count replace the defaults.</summary>
    [Fact]
    public void FormatNumber_ConfiguredPrefixAndDigits_UsesThem()
    {
        var options = new OrderOptions { NumberPrefix = "HP", NumberDigits = 6 };

        options.FormatNumber(7).Should().Be("HP000007");
    }

    /// <summary>The payment term is the configured number of days.</summary>
    [Fact]
    public void InvoicePaymentTerm_ConfiguredDays_IsThatManyDays()
    {
        var options = new OrderOptions { InvoicePaymentTermDays = 30 };

        options.InvoicePaymentTerm.Should().Be(TimeSpan.FromDays(30));
    }

    /// <summary>Values that cannot number an order or date its invoice are refused.</summary>
    /// <param name="prefix">The configured prefix.</param>
    /// <param name="digits">The configured digit count.</param>
    /// <param name="termDays">The configured payment term in days.</param>
    [Theory]
    [InlineData("ORD-", 0, 7)]
    [InlineData("ORD-", 4, -1)]
    [InlineData("ORD-", 4, 366)]
    [InlineData("A-VERY-LONG-PREFIX-", 4, 7)]
    public void IsValid_UnusableValues_IsFalse(string prefix, int digits, int termDays)
    {
        var options = new OrderOptions
        {
            NumberPrefix = prefix,
            NumberDigits = digits,
            InvoicePaymentTermDays = termDays,
        };

        options.IsValid.Should().BeFalse();
    }

    /// <summary>The retry lease defaults to ten minutes, and the minutes configured become the lease.</summary>
    [Fact]
    public void RetryLease_DefaultsToTenMinutesAndFollowsTheConfiguredMinutes()
    {
        new OrderOptions().RetryLease.Should().Be(TimeSpan.FromMinutes(10));
        new OrderOptions { RetryLeaseMinutes = 45 }.RetryLease.Should().Be(TimeSpan.FromMinutes(45));
    }

    /// <summary>A retry lease that is zero, negative or longer than a day cannot be used and is refused at startup.</summary>
    /// <param name="minutes">The configured lease in minutes.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(OrderOptions.MaxRetryLeaseMinutes + 1)]
    public void IsValid_UnusableRetryLease_IsFalse(int minutes)
    {
        new OrderOptions { RetryLeaseMinutes = minutes }.IsValid.Should().BeFalse();
    }

    /// <summary>The shortest and the longest accepted retry lease are valid.</summary>
    /// <param name="minutes">The configured lease in minutes.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(OrderOptions.MaxRetryLeaseMinutes)]
    public void IsValid_RetryLeaseAtTheLimits_IsTrue(int minutes)
    {
        new OrderOptions { RetryLeaseMinutes = minutes }.IsValid.Should().BeTrue();
    }

    /// <summary>A prefix and padding that exactly fill the order number column are accepted.</summary>
    [Fact]
    public void IsValid_PrefixAndDigitsFillingTheColumn_IsTrue()
    {
        var options = new OrderOptions
        {
            NumberPrefix = new string('A', Order.OrderNumberMaxLength - 4),
            NumberDigits = 4,
        };

        options.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// A sequence that outgrows its padding stops fitting the order number column even though the
    /// options themselves are valid, so the number is checked when it is handed out.
    /// </summary>
    [Fact]
    public void Fits_SequenceOutgrowingTheColumn_IsFalse()
    {
        var options = new OrderOptions
        {
            NumberPrefix = new string('A', Order.OrderNumberMaxLength - 4),
            NumberDigits = 4,
        };

        options.IsValid.Should().BeTrue();
        options.Fits(9_999).Should().BeTrue();
        options.Fits(10_000).Should().BeFalse();
    }
}
