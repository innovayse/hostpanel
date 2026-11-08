using Innovayse.Hostpanel.Application.Admin.Integrations.Commands.SaveIntegrationConfig;
using Innovayse.Hostpanel.Application.Admin.Integrations.Commands.TestIntegrationConnection;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetHostingPanelServerInfo;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.ListIntegrations;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Staff administration of third-party integration configurations. Credentials are stored as
/// key-value settings; a secret field is write only and reads back as <c>isSecret</c> and
/// <c>hasValue</c>, never as its value.
/// </summary>
[Route("api/admin/integrations")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class IntegrationsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public IntegrationsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>The enabled/configured status for every known integration.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the integrations; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<IntegrationListItemDto>>>(
            new ListIntegrationsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>The full configuration for one integration; secret fields carry no value.</summary>
    /// <param name="slug">URL-safe integration identifier, e.g. card.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the integration's detail; 404 when the slug is not recognised; 401 without a session; 403 for a caller who is not an admin; 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(IntegrationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(string slug, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IntegrationDetailDto>>(new GetIntegrationQuery(slug), ct);
        return HandleResult(outcome);
    }

    /// <summary>Saves (upserts) the configuration for one integration.</summary>
    /// <param name="slug">URL-safe integration identifier; wins over any slug in the body.</param>
    /// <param name="command">
    /// Whether the integration is enabled, its field values and the fields to clear. An empty
    /// secret field keeps the stored value.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 400 with <c>INTEGRATION_FIELD_UNKNOWN</c> when a written or cleared key is not declared by the integration (exact spelling; <c>is_enabled</c> is reserved) or <c>INTEGRATION_FIELD_INVALID</c> when a value does not fit its field's type, with nothing written; 404 with <c>INTEGRATION_NOT_FOUND</c> when the slug is not recognised; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPut("{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveAsync(
        string slug, [FromBody] SaveIntegrationConfigCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Slug = slug }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Tests whether all required credential fields are configured for the integration, and, for
    /// integrations that support it, runs a live probe against the provider.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the test outcome, whether or not the probe itself succeeded; 404 with <c>INTEGRATION_NOT_FOUND</c> when the slug is not recognised; 401 without a session; 403 for a caller who is not an admin; 400 with <c>INTEGRATION_NOT_TESTABLE</c> when the integration has no way to be tested; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{slug}/test")]
    [ProducesResponseType(typeof(IntegrationTestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestAsync(string slug, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IntegrationTestResultDto>>(
            new TestIntegrationConnectionCommand(slug), ct);
        return HandleResult(outcome);
    }

    /// <summary>Live hosting panel server status (cached 5 minutes).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the connection status, account count and version; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet("cwp/server-info")]
    [ProducesResponseType(typeof(HostingPanelServerInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHostingPanelServerInfoAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<HostingPanelServerInfoDto>>(new GetHostingPanelServerInfoQuery(), ct);
        return HandleResult(outcome);
    }
}
