namespace Innovayse.API.Domains;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.CheckDomainAvailability;
using Innovayse.Application.Domains.Queries.GetTldPricing;
using Innovayse.Application.Domains.Queries.GetWhois;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Public domain lookup endpoints accessible to any authenticated user (Admin, Reseller, or Client).
/// These are read-only queries that do not require elevated privileges.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching queries.</param>
[ApiController]
[Route("api/domains")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller},{Roles.Client}")]
public sealed class DomainLookupController(IMessageBus bus) : ControllerBase
{
    /// <summary>Checks whether a domain name is available for registration (GET).</summary>
    /// <param name="name">The fully-qualified domain name to check (e.g. "example.com").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    [HttpGet("check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckAvailabilityAsync(
        [FromQuery] string name,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<bool>(new CheckDomainAvailabilityQuery(name), ct);
        return Ok(result);
    }

    /// <summary>Checks whether a domain name is available for registration (POST).</summary>
    /// <param name="request">Request body containing the domain name to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="DomainCheckResultDto"/> with the domain name, availability flag, and status.</returns>
    [HttpPost("check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DomainCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DomainCheckResultDto>> CheckAvailabilityPostAsync(
        [FromBody] CheckDomainRequest request,
        CancellationToken ct)
    {
        var domain = request.Domain?.Trim();

        if (string.IsNullOrWhiteSpace(domain))
        {
            return BadRequest(new { error = "Domain name is required." });
        }

        var available = await bus.InvokeAsync<bool>(new CheckDomainAvailabilityQuery(domain), ct);
        var status = available ? "available" : "taken";

        return Ok(new DomainCheckResultDto(domain, available, status));
    }

    /// <summary>Returns TLD pricing information for domain registration, transfer, and renewal.</summary>
    /// <param name="currency">Target currency code (e.g. "USD", "RUB", "AMD"). Defaults to USD.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>TLD pricing data with currency and per-extension price breakdowns.</returns>
    [HttpGet("tld-pricing")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TldPricingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TldPricingDto>> GetTldPricingAsync(
        [FromQuery] string? currency,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<TldPricingDto>(new GetTldPricingQuery(currency), ct);
        return Ok(result);
    }
}

/// <summary>Request body for the POST domain availability check endpoint.</summary>
public sealed class CheckDomainRequest
{
    /// <summary>The fully-qualified domain name to check (e.g. "example.com").</summary>
    public required string Domain { get; init; }
}
