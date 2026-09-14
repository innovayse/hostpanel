namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Billing.Currencies.Commands.SetBaseCurrency;
using Innovayse.Application.Billing.Currencies.Commands.UpdateExchangeRates;
using Innovayse.Application.Billing.Currencies.Commands.UpdateProductPrices;
using Innovayse.Application.Billing.Currencies.Commands.UpsertCurrency;
using Innovayse.Application.Billing.Currencies.Queries.ListCurrencies;
using Innovayse.Application.Billing.Currencies.Queries.ListPublicCurrencies;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Currency administration — the configured currencies, their rates, which one is the base, and
/// the bulk price recompute — plus the one anonymous list the storefront's currency picker reads.
/// Every route under <c>api/admin/currencies</c> requires the Admin role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/currencies")]
[Authorize(Roles = Roles.Admin)]
public sealed class CurrenciesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns every configured currency, base first, rates included.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per currency.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<CurrencyDto>>(new ListCurrenciesQuery(), ct);
        return Ok(result);
    }

    /// <summary>Creates the currency under the code, or rewrites the one already there.</summary>
    /// <param name="code">ISO 4217 alpha code, any case.</param>
    /// <param name="request">The currency's numeric code, display, rate and switch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{code}")]
    public async Task<IActionResult> UpsertAsync(string code, [FromBody] UpsertCurrencyRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpsertCurrencyCommand(
                code, request.Numeric, request.Prefix, request.Suffix, request.Decimals, request.RateToBase, request.IsEnabled),
            ct);
        return NoContent();
    }

    /// <summary>Makes the currency the base and re-expresses every other rate against it.</summary>
    /// <param name="code">ISO 4217 alpha code, any case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{code}/make-base")]
    public async Task<IActionResult> MakeBaseAsync(string code, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetBaseCurrencyCommand(code), ct);
        return NoContent();
    }

    /// <summary>Refreshes every non-base rate from the exchange-rate source.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Which codes were updated and which the source did not quote.</returns>
    [HttpPost("update-rates")]
    public async Task<ActionResult<UpdateExchangeRatesResultDto>> UpdateRatesAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<UpdateExchangeRatesResultDto>(new UpdateExchangeRatesCommand(), ct);
        return Ok(result);
    }

    /// <summary>Recomputes every product's prices in the named currencies from its base prices.</summary>
    /// <param name="request">The currencies to recompute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many price rows were written.</returns>
    [HttpPost("update-product-prices")]
    public async Task<ActionResult<int>> UpdateProductPricesAsync(
        [FromBody] UpdateProductPricesRequest request, CancellationToken ct)
    {
        var written = await bus.InvokeAsync<int>(new UpdateProductPricesCommand(request.Currencies), ct);
        return Ok(written);
    }

    /// <summary>Returns the currencies a visitor may be billed in, without rates, for the storefront.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per enabled currency, base first.</returns>
    [HttpGet("/api/currencies")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicCurrencyDto>>> ListPublicAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<PublicCurrencyDto>>(new ListPublicCurrenciesQuery(), ct);
        return Ok(result);
    }
}
