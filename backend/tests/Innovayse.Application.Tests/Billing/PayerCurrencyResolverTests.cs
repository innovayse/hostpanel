namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Services;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Xunit;

/// <summary>The single rule for "which currency is this payer billed in".</summary>
public sealed class PayerCurrencyResolverTests
{
    private readonly Mock<ICurrencyRepository> currencies = new();
    private readonly Mock<IClientRepository> clients = new();
    private readonly Mock<ICurrentRequestContext> caller = new();
    private readonly Currency usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);
    private readonly Currency amd = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);

    public PayerCurrencyResolverTests()
    {
        currencies.Setup(c => c.GetBaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
    }

    private PayerCurrencyResolver Resolver() => new(currencies.Object, clients.Object, caller.Object);

    private Client ClientBilledIn(string? currency)
    {
        var c = Client.Create("user-1", "Jane", "Doe", "jane@example.com");
        if (currency is not null)
        {
            c.SetCurrency(currency);
        }

        return c;
    }

    /// <summary>A client with a currency is billed in it.</summary>
    [Fact]
    public async Task ForClient_UsesTheClientsCurrency()
    {
        clients.Setup(r => r.FindByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(ClientBilledIn("AMD"));
        var c = await Resolver().ForClientAsync(3, CancellationToken.None);
        Assert.Equal("AMD", c.Code);
    }

    /// <summary>A client with none recorded is billed in the base.</summary>
    [Fact]
    public async Task ForClient_FallsBackToBase()
    {
        clients.Setup(r => r.FindByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(ClientBilledIn(null));
        var c = await Resolver().ForClientAsync(3, CancellationToken.None);
        Assert.Equal("USD", c.Code);
    }

    /// <summary>
    /// A client whose recorded currency is not configured is billed in the base rather than in
    /// something the panel cannot format or charge. It is logged as a data problem, not hidden.
    /// </summary>
    [Fact]
    public async Task ForClient_UnknownCurrencyFallsBackToBase()
    {
        clients.Setup(r => r.FindByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(ClientBilledIn("XYZ"));
        currencies.Setup(c => c.FindAsync("XYZ", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        var c = await Resolver().ForClientAsync(3, CancellationToken.None);
        Assert.Equal("USD", c.Code);
    }

    /// <summary>An anonymous caller (guest checkout) is billed in the base.</summary>
    [Fact]
    public async Task ForCaller_GuestIsBase()
    {
        caller.SetupGet(c => c.UserId).Returns((string?)null);
        var c = await Resolver().ForCallerAsync(CancellationToken.None);
        Assert.Equal("USD", c.Code);
    }

    /// <summary>A signed-in caller is billed in their client record's currency.</summary>
    [Fact]
    public async Task ForCaller_SignedInUsesTheirClient()
    {
        caller.SetupGet(c => c.UserId).Returns("user-1");
        clients.Setup(r => r.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(ClientBilledIn("AMD"));
        var c = await Resolver().ForCallerAsync(CancellationToken.None);
        Assert.Equal("AMD", c.Code);
    }
}
