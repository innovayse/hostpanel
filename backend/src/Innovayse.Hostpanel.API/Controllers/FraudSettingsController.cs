using Innovayse.Hostpanel.Application.Fraud.Commands.UpdateFraudSettings;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Queries.GetFraudSettings;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for the fraud detection settings: the switch, the two thresholds and the velocity and shared IP limits.</summary>
[Route("api/admin/fraud/settings")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class FraudSettingsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public FraudSettingsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the fraud detection settings; whatever was never saved reads as its default.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the settings; 401 without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(FraudSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FraudSettingsDto>>(new GetFraudSettingsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Saves the fraud detection settings, all of them together.</summary>
    /// <param name="command">The settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when saved; 400 with <c>VALIDATION_ERROR</c> when a score is outside 0 to 100, the review score is not below the reject
    /// score or a limit is outside its range; 401 without a session; 403 for a caller who is not an administrator; 404 with
    /// <c>SETTING_NOT_FOUND</c> when a setting row was never seeded;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateFraudSettingsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }
}
