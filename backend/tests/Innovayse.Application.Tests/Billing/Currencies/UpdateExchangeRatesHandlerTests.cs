namespace Innovayse.Application.Tests.Billing.Currencies;

using Innovayse.Application.Billing.Currencies.Commands.UpdateExchangeRates;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Moq;
using Xunit;

/// <summary>"Update rates" writes what the source quotes and leaves alone what it does not.</summary>
public sealed class UpdateExchangeRatesHandlerTests
{
    /// <summary>The base; its rate is never asked for.</summary>
    private readonly Currency usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);

    /// <summary>A currency the source quotes.</summary>
    private readonly Currency amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025m, isBase: false);

    /// <summary>A currency the source does not quote.</summary>
    private readonly Currency gel = Currency.Create("GEL", "981", "₾", string.Empty, 2, 0.37m, isBase: false);

    /// <summary>The codes the handler asked the source for.</summary>
    private IEnumerable<string>? asked;

    /// <summary>Wires the handler over a source quoting only AMD.</summary>
    /// <returns>The handler under test.</returns>
    private UpdateExchangeRatesHandler Handler()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(c => c.ListAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([usd, amd, gel]);

        var source = new Mock<IExchangeRateSource>();
        source.Setup(s => s.RatesToAsync("USD", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((_, codes, _) => asked = codes.ToList())
            .ReturnsAsync(new Dictionary<string, decimal> { ["AMD"] = 0.0027m });

        return new UpdateExchangeRatesHandler(currencies.Object, source.Object, Mock.Of<IUnitOfWork>());
    }

    /// <summary>Quoted codes are written; unquoted ones keep their rate and are reported as missing.</summary>
    [Fact]
    public async Task Handle_WritesQuotedRatesAndReportsTheRest()
    {
        var result = await Handler().HandleAsync(new UpdateExchangeRatesCommand(), CancellationToken.None);

        Assert.Equal(["AMD"], result.Updated);
        Assert.Equal(["GEL"], result.Missing);
        Assert.Equal(0.0027m, amd.RateToBase);
        Assert.Equal(0.37m, gel.RateToBase);
        Assert.Equal(1m, usd.RateToBase);
    }

    /// <summary>Only the non-base codes are asked for; the base's rate to itself is not a question.</summary>
    [Fact]
    public async Task Handle_AsksOnlyForTheNonBaseCodes()
    {
        await Handler().HandleAsync(new UpdateExchangeRatesCommand(), CancellationToken.None);

        Assert.NotNull(asked);
        Assert.Equal(["AMD", "GEL"], asked!.Order());
    }
}
