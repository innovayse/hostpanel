namespace Innovayse.Application.Tests.Billing.Currencies;

using Innovayse.Application.Billing.Currencies.Commands.UpsertCurrency;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Moq;
using Xunit;

/// <summary>PUT on a currency code creates the row or rewrites the one that is there.</summary>
public sealed class UpsertCurrencyHandlerTests
{
    /// <summary>The base, which keeps its rate whatever the form posts.</summary>
    private readonly Currency usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);

    /// <summary>An existing non-base row.</summary>
    private readonly Currency amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025m, isBase: false);

    /// <summary>The rows the handler added.</summary>
    private readonly List<Currency> added = [];

    /// <summary>Wires the handler over USD and AMD.</summary>
    /// <returns>The handler under test.</returns>
    private UpsertCurrencyHandler Handler()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("EUR", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        currencies.Setup(c => c.Add(It.IsAny<Currency>())).Callback<Currency>(added.Add);
        return new UpsertCurrencyHandler(currencies.Object, Mock.Of<IUnitOfWork>());
    }

    /// <summary>A code with no row gets one, never as the base.</summary>
    [Fact]
    public async Task Handle_CreatesAMissingCurrency()
    {
        await Handler().HandleAsync(
            new UpsertCurrencyCommand("eur", "978", "€", string.Empty, 2, 1.1m, IsEnabled: true), CancellationToken.None);

        var eur = Assert.Single(added);
        Assert.Equal("EUR", eur.Code);
        Assert.Equal(1.1m, eur.RateToBase);
        Assert.False(eur.IsBase);
        Assert.True(eur.IsEnabled);
    }

    /// <summary>An existing row takes the new display, rate and switch; nothing is added.</summary>
    [Fact]
    public async Task Handle_RewritesAnExistingCurrency()
    {
        await Handler().HandleAsync(
            new UpsertCurrencyCommand("AMD", "051", "֏", string.Empty, 2, 0.0026m, IsEnabled: false), CancellationToken.None);

        Assert.Empty(added);
        Assert.Equal("֏", amd.Prefix);
        Assert.Equal(2, amd.Decimals);
        Assert.Equal(0.0026m, amd.RateToBase);
        Assert.False(amd.IsEnabled);
    }

    /// <summary>The base's rate to itself is 1 whatever is posted, and it cannot be switched off.</summary>
    [Fact]
    public async Task Handle_KeepsTheBaseAtOneAndEnabled()
    {
        await Handler().HandleAsync(
            new UpsertCurrencyCommand("USD", "840", "US$", string.Empty, 2, 2m, IsEnabled: true), CancellationToken.None);

        Assert.Equal("US$", usd.Prefix);
        Assert.Equal(1m, usd.RateToBase);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(
                new UpsertCurrencyCommand("USD", "840", "$", string.Empty, 2, 1m, IsEnabled: false), CancellationToken.None));
    }
}
