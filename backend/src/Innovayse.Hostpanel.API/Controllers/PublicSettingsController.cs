using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetPublicLegalText;
using Innovayse.Hostpanel.Application.Admin.Queries.GetPublicSettings;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Storefront-facing settings, readable without a login.
/// <para>
/// Separate from <see cref="SettingsController"/> rather than an extra action on it: that
/// controller requires <see cref="Innovayse.Hostpanel.Shared.Authorization.Policies.Admin"/> at
/// the class level and returns whole setting rows, and the settings table holds integration
/// credentials. Keeping the public read in its own controller means the anonymous path can never
/// widen by someone adding a field to the admin DTO.
/// </para>
/// </summary>
[Route("api/settings")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PublicSettingsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PublicSettingsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the storefront's settings: template choice and contact channels.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the allow-listed keys that have a value; no failure status is possible; 429 when the rate limit is hit.</returns>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<PublicSettingDto>>>(
            new GetPublicSettingsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the operator's legal text of one storefront page, in each language it is written in.</summary>
    /// <param name="page">The page slug: <c>terms</c>, <c>privacy</c>, <c>refund</c>, <c>acceptable_use</c> or <c>cookies</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page's rows that have a value (empty when the operator wrote none); 404 when the page is unknown; 429 when the rate limit is hit.</returns>
    [HttpGet("public/legal/{page}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicLegalTextAsync(string page, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<PublicSettingDto>>>(
            new GetPublicLegalTextQuery(page), ct);
        return HandleResult(outcome);
    }
}
