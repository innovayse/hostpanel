using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetTldRenewPrice;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="GetTldRenewPriceQuery"/> through the real <see cref="IMessageBus"/>: the configured
/// renew sell price for a period, in the TLD's own currency.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTldRenewPriceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A configured period answers its price and the TLD's sell currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ConfiguredPeriod_AnswersThePriceAsync()
    {
        var tld = await SeedTldAsync(new Dictionary<int, decimal> { [1] = 14.5m });

        var result = await InvokeAsync(new GetTldRenewPriceQuery($"example.{tld}", 1));

        result.Value.Should().Be(new TldRenewPriceDto(tld, "EUR", 14.5m));
    }

    /// <summary>A period the TLD sets no renew price for answers a null price.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnpricedPeriod_AnswersNoPriceAsync()
    {
        var tld = await SeedTldAsync(new Dictionary<int, decimal> { [1] = 14.5m });

        var result = await InvokeAsync(new GetTldRenewPriceQuery($"example.{tld}", 2));

        result.IsSuccess.Should().BeTrue();
        result.Value!.SellPrice.Should().BeNull();
    }

    /// <summary>A name whose TLD is not configured is refused with <c>TLD_CONFIG_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownTld_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new GetTldRenewPriceQuery($"example.{Guid.NewGuid():N}"[..20], 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
    }

    /// <summary>Seeds a TLD under a unique extension, sold in EUR, with the given renew prices.</summary>
    /// <param name="sellRenew">Renew prices keyed by years.</param>
    /// <returns>The extension.</returns>
    private async Task<string> SeedTldAsync(Dictionary<int, decimal> sellRenew)
    {
        var tld = $"r{Guid.NewGuid():N}"[..10];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var config = TldConfig.Create(tld, RegistrarModule.NameAm, "USD", "EUR", true, 0, []);
        config.UpdateSellPrices([], [], sellRenew);
        context.TldConfigs.Add(config);
        await context.SaveChangesAsync(CancellationToken.None);
        return tld;
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<TldRenewPriceDto>> InvokeAsync(GetTldRenewPriceQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<TldRenewPriceDto>>(query);
    }
}
