using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ConvertAmounts;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// <see cref="ConvertAmountsQuery"/> through the real <see cref="IMessageBus"/>: the conversion other
/// modules use instead of reading the currency table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConvertAmountsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An amount in a stored currency is converted through the rates; an unknown currency is left as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ConvertsKnownAndLeavesUnknownAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "XTS", "963", 0.5m);
        using var scope = factory.Services.CreateScope();
        var baseCode = await BaseCodeAsync(scope);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<ConvertedAmountsDto>>(new ConvertAmountsQuery(
            "XTS", [new CurrencyAmountDto(10m, baseCode), new CurrencyAmountDto(7m, "QQQ")]));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Currency.Should().Be("XTS");
        result.Value.Amounts.Should().Equal(new ConvertedAmountDto(20m, 2), new ConvertedAmountDto(7m, 2));
    }

    /// <summary>
    /// A target currency with no stored row is refused with <c>CURRENCY_NOT_FOUND</c> instead of
    /// answering unconverted amounts labelled with the requested code.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnconfiguredTarget_ReturnsCurrencyNotFoundAsync()
    {
        using var scope = factory.Services.CreateScope();
        var baseCode = await BaseCodeAsync(scope);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<ConvertedAmountsDto>>(
            new ConvertAmountsQuery("XXQ", [new CurrencyAmountDto(3m, baseCode)]));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
    }

    /// <summary>With no target named, the base currency is the target.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoTarget_UsesTheBaseCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var baseCode = await BaseCodeAsync(scope);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<ConvertedAmountsDto>>(
            new ConvertAmountsQuery(null, [new CurrencyAmountDto(3m, baseCode)]));

        result.Value!.Currency.Should().Be(baseCode);
        result.Value.Amounts.Should().ContainSingle().Which.Amount.Should().Be(3m);
    }

    /// <summary>
    /// With no target named and no base currency stored, the query refuses with
    /// <c>BASE_CURRENCY_NOT_CONFIGURED</c> instead of answering in an empty currency code. Runs on a
    /// factory of its own, since unmarking the base currency would change every other test's database.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoTargetAndNoBaseCurrency_ReturnsBaseCurrencyNotConfiguredAsync()
    {
        await using var isolated = new IntegrationTestFactory();
        await isolated.InitializeAsync();
        await BillingTestSeeds.RemoveBaseCurrencyAsync(isolated);
        using var scope = isolated.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<ConvertedAmountsDto>>(
            new ConvertAmountsQuery(null, [new CurrencyAmountDto(3m, "USD")]));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.BASE_CURRENCY_NOT_CONFIGURED);
    }

    /// <summary>Reads the stored base currency's code.</summary>
    /// <param name="scope">The scope to read through.</param>
    /// <returns>The base currency code.</returns>
    private static async Task<string> BaseCodeAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Currencies.AsNoTracking()
            .Where(c => c.IsBase)
            .Select(c => c.Code)
            .FirstAsync(CancellationToken.None);
    }
}
