namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;
using Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;
using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;
using Innovayse.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Application.Admin.Integrations.Queries.ListIntegrations;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Wolverine;

/// <summary>
/// Admin endpoints for managing third-party integration configurations.
/// Credentials are stored as key-value Settings; secret fields are masked on read.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="cache">In-memory cache for short-lived server status results.</param>
[ApiController]
[Route("api/admin/integrations")]
[Authorize(Roles = Roles.Admin)]
public sealed class IntegrationsController(IMessageBus bus, IMemoryCache cache) : ControllerBase
{
    /// <summary>
    /// Returns the enabled/configured status for all 10 integrations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of integration summary items.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationListItemDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<IntegrationListItemDto>>(
            new ListIntegrationsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns the full configuration for one integration with secret fields masked.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. stripe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Integration detail DTO, or 404 when the slug is not recognised.</returns>
    [HttpGet("{slug}")]
    public async Task<ActionResult<IntegrationDetailDto>> GetAsync(string slug, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IntegrationDetailDto?>(
            new GetIntegrationQuery(slug), ct);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Saves (upserts) the configuration for one integration.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. smtp.</param>
    /// <param name="req">Request body containing IsEnabled and Config.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{slug}")]
    public async Task<IActionResult> SaveAsync(
        string slug,
        [FromBody] SaveIntegrationConfigRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(
            new SaveIntegrationConfigCommand(slug, req.IsEnabled, req.Config), ct);

        if (slug.Equals("cwp", StringComparison.OrdinalIgnoreCase))
        {
            cache.Remove("cwp:server-info");
        }

        return NoContent();
    }

    /// <summary>
    /// Tests whether all required credential fields are configured for the integration.
    /// Does not make a live network call.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. cpanel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result DTO with success flag, message, and timestamp.</returns>
    [HttpPost("{slug}/test")]
    public async Task<ActionResult<IntegrationTestResultDto>> TestAsync(string slug, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IntegrationTestResultDto>(
            new TestIntegrationConnectionCommand(slug), ct);

        if (slug.Equals("cwp", StringComparison.OrdinalIgnoreCase))
        {
            cache.Remove("cwp:server-info");
        }

        return Ok(result);
    }

    /// <summary>Returns live CWP server status (cached 5 minutes).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>CWP server info including connection status, account count, and version.</returns>
    [HttpGet("cwp/server-info")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CwpServerInfoDto>> GetCwpServerInfoAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CwpServerInfoDto>(new GetCwpServerInfoQuery(), ct);
        return Ok(result);
    }
}
