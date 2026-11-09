using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetTldPricing;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Public domain lookup endpoints. Domain availability checking lives in
/// <see cref="Innovayse.Hostpanel.API.Controllers.DomainsController"/>.
/// </summary>
[Route("api/domains")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class DomainLookupController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public DomainLookupController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns TLD pricing information for domain registration, transfer, and renewal.</summary>
    /// <param name="currency">Target currency code (e.g. "USD", "RUB", "AMD"). Defaults to the base currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with TLD pricing data and per-extension price breakdowns; 503 with
    /// <c>BASE_CURRENCY_NOT_CONFIGURED</c> when no currency is asked for and no base currency is stored;
    /// 404 with <c>CURRENCY_NOT_FOUND</c> when the asked-for currency is not configured.
    /// Anonymous: the storefront and client area both need it before or without a session;
    /// 400 when the body, query or route fails validation; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("tld-pricing")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(TldPricingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTldPricingAsync([FromQuery] string? currency, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TldPricingDto>>(new GetTldPricingQuery(currency), ct);
        return HandleResult(outcome);
    }
}
