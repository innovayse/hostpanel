namespace Innovayse.API.Admin;

using Innovayse.Application.Domains.Commands.CreateTldConfig;
using Innovayse.Application.Domains.Commands.DeleteTldConfig;
using Innovayse.Application.Domains.Commands.ImportTldPricing;
using Innovayse.Application.Domains.Commands.SyncTldCostPrices;
using Innovayse.Application.Domains.Commands.UpdateTldConfig;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.GetTldConfig;
using Innovayse.Application.Domains.Queries.ListTldConfigs;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing TLD configurations (CRUD, import from registrars, and cost-price sync).
/// All operations require the Admin role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/tld-configs")]
[Authorize(Roles = Roles.Admin)]
public sealed class TldConfigsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns all TLD configurations for the admin list view.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all TLD configurations with summary pricing data.</returns>
    [HttpGet]
    public async Task<ActionResult<List<TldConfigListItemDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<List<TldConfigListItemDto>>(new ListTldConfigsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single TLD configuration by ID with full pricing details.
    /// </summary>
    /// <param name="id">The TLD configuration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The TLD configuration DTO, or 404 when not found.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TldConfigDto>> GetAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<TldConfigDto?>(new GetTldConfigQuery(id), ct);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates a new TLD configuration.
    /// </summary>
    /// <param name="cmd">The create TLD configuration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new TLD configuration's ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateTldConfigCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return CreatedAtAction(nameof(GetAsync), new { id }, id);
    }

    /// <summary>
    /// Updates an existing TLD configuration's settings and pricing.
    /// </summary>
    /// <param name="id">The TLD configuration identifier from the route.</param>
    /// <param name="cmd">The update TLD configuration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success, or 400 Bad Request when the route ID does not match the command ID.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateTldConfigCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id)
        {
            return BadRequest("Route ID does not match command ID.");
        }

        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a TLD configuration by ID.
    /// </summary>
    /// <param name="id">The TLD configuration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteTldConfigCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Imports TLD pricing from the specified registrar module, creating or updating TLD configurations.
    /// </summary>
    /// <param name="module">The registrar module name to import from (e.g. "NameAm", "Namecheap").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import result with counts of imported and updated TLDs.</returns>
    [HttpPost("import/{module}")]
    public async Task<ActionResult<ImportTldPricingResult>> ImportAsync(string module, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ImportTldPricingResult>(new ImportTldPricingCommand(module), ct);
        return Ok(result);
    }

    /// <summary>
    /// Triggers an immediate sync of registrar cost prices for all TLD configurations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncAsync(CancellationToken ct)
    {
        await bus.InvokeAsync(new SyncTldCostPricesCommand(), ct);
        return Ok();
    }
}
