namespace Innovayse.Application.Tests.Billing.Currencies;

using Innovayse.Application.Billing.Currencies.Commands.SetBaseCurrency;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Moq;
using Xunit;

/// <summary>Making another currency the base demotes the old one and re-expresses every rate.</summary>
public sealed class SetBaseCurrencyHandlerTests
{
    /// <summary>The rate of the currency about to become the base: 1 AMD = 0.0025641 USD, so 1 USD = 390 AMD.</summary>
    private const decimal AmdRate = 0.0025641m;

    /// <summary>What a USD is worth in AMD once AMD is the base, to the precision the rate carries.</summary>
    private const decimal UsdInAmd = 390m;

    /// <summary>The current base.</summary>
    private readonly Currency usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);

    /// <summary>The currency to promote.</summary>
    private readonly Currency amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, AmdRate, isBase: false);

    /// <summary>A bystander, whose rate must be re-expressed too: 1 EUR = 1.1 USD.</summary>
    private readonly Currency eur = Currency.Create("EUR", "978", "€", string.Empty, 2, 1.1m, isBase: false);

    /// <summary>Records whether the handler saved.</summary>
    private readonly Mock<IUnitOfWork> uow = new();

    /// <summary>Wires the handler over the three currencies.</summary>
    /// <returns>The handler under test.</returns>
    private SetBaseCurrencyHandler Handler()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(c => c.ListAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([usd, amd, eur]);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("XXX", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        return new SetBaseCurrencyHandler(currencies.Object, uow.Object);
    }

    /// <summary>With USD base and AMD at 0.0025641, making AMD base gives USD a rate of 390.</summary>
    [Fact]
    public async Task Handle_ReexpressesRatesAgainstTheNewBase()
    {
        await Handler().HandleAsync(new SetBaseCurrencyCommand("AMD"), CancellationToken.None);

        Assert.True(amd.IsBase);
        Assert.Equal(1m, amd.RateToBase);
        Assert.False(usd.IsBase);
        Assert.Equal(UsdInAmd, Math.Round(usd.RateToBase, 0));
        Assert.Equal(Math.Round(1.1m / AmdRate, 4), Math.Round(eur.RateToBase, 4));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Promoting the currency that is already the base changes nothing and is not an error.</summary>
    [Fact]
    public async Task Handle_PromotingTheBaseAgainIsANoOp()
    {
        await Handler().HandleAsync(new SetBaseCurrencyCommand("USD"), CancellationToken.None);

        Assert.True(usd.IsBase);
        Assert.Equal(AmdRate, amd.RateToBase);
        Assert.Equal(1.1m, eur.RateToBase);
    }

    /// <summary>A code that is not configured cannot become the base.</summary>
    [Fact]
    public async Task Handle_RefusesAnUnconfiguredCurrency()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(new SetBaseCurrencyCommand("XXX"), CancellationToken.None));

        Assert.True(usd.IsBase);
    }
}
