using Innovayse.Hostpanel.Application.Domains.Commands.CreateTldConfig;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteTldConfig;
using Innovayse.Hostpanel.Application.Domains.Commands.ImportTldPricing;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncTldCostPricesNow;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateTldConfig;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetTldConfig;
using Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for managing TLD configurations (CRUD, import from registrars, and cost-price sync).
/// </summary>
[Route("api/admin/tld-configs")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class TldConfigsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public TldConfigsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a page of TLD configurations for the admin list view.</summary>
    /// <param name="query">Pagination, optional TLD search and optional registrar and enabled filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the page, the page size, the search or a filter is refused;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PaginatedList<TldConfigListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListTldConfigsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<TldConfigListItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single TLD configuration by ID with full pricing details.</summary>
    /// <param name="id">The TLD configuration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the configuration; 400 when a value is refused; 404 when the id names no configuration;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(TldConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TldConfigDto>>(new GetTldConfigQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new TLD configuration.</summary>
    /// <param name="command">The create TLD configuration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new configuration's id; 400 when a field is refused or the registrar module is unknown;
    /// 409 when the TLD is already configured; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTldConfigCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/tld-configs/{id}", id));
    }

    /// <summary>Updates an existing TLD configuration's settings and pricing.</summary>
    /// <param name="id">The TLD configuration identifier from the route; wins over any id in the body.</param>
    /// <param name="command">The update TLD configuration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a field is refused or the registrar module is unknown;
    /// 404 when the id names no configuration; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateTldConfigCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a TLD configuration by ID.</summary>
    /// <param name="id">The TLD configuration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 404 when the id names no configuration;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteTldConfigCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Imports TLD pricing from the specified registrar module, creating or updating TLD configurations.</summary>
    /// <param name="module">The id of the registrar plugin to import from, in any letter case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the import result; 400 when the registrar module is unknown;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 502 when the registrar refuses to list its prices; 503 with <c>REGISTRAR_ACCESS_DENIED</c> when the registrar
    /// refuses the saved credentials; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("import/{module}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ImportTldPricingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ImportAsync(string module, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ImportTldPricingResult>>(new ImportTldPricingCommand(module), ct);
        return HandleResult(outcome);
    }

    /// <summary>Triggers an immediate sync of registrar cost prices for all TLD configurations.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 401 when the caller is not signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.</returns>
    [HttpPost("sync")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new SyncTldCostPricesNowCommand(), ct);
        return HandleResult(outcome);
    }
}
