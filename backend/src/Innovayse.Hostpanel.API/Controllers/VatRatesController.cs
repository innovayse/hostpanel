using Innovayse.Hostpanel.Application.Billing.VatRates.Commands.CreateVatRate;
using Innovayse.Hostpanel.Application.Billing.VatRates.Commands.DeleteVatRate;
using Innovayse.Hostpanel.Application.Billing.VatRates.Commands.UpdateVatRate;
using Innovayse.Hostpanel.Application.Billing.VatRates.Common;
using Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin API for VAT rates: the standard and reduced rate of each EU member state, with the day each applies from.</summary>
[Route("api/admin/vat-rates")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class VatRatesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public VatRatesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>A page of VAT rates.</summary>
    /// <param name="query">Pagination and an optional search on the country code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page; 400 when the page, the page size or the search is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<VatRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListVatRatesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<VatRateDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds the VAT rate of an EU member state from a given day on.</summary>
    /// <param name="command">The row's country, kind, rate and first day.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new row's id; 400 when a field is refused; 409 with <c>VAT_RATE_ALREADY_EXISTS</c> when the country, kind and day have a row; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateVatRateCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/vat-rates/{id}", id));
    }

    /// <summary>Replaces a VAT rate row's country, kind, rate and first day.</summary>
    /// <param name="id">The row's primary key; wins over any id in the body.</param>
    /// <param name="command">The row's new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>VAT_RATE_NOT_FOUND</c>; 409 with <c>VAT_RATE_ALREADY_EXISTS</c> when another row holds the country, kind and day; 400 when a field is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateVatRateCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a VAT rate row. Invoices already issued keep the rate they were taxed at.</summary>
    /// <param name="id">The row's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>VAT_RATE_NOT_FOUND</c>; 400 when the route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteVatRateCommand(id), ct);
        return HandleResult(outcome);
    }
}
