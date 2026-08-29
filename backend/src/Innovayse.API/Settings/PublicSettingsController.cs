namespace Innovayse.API.Settings;

using Innovayse.Application.Admin.Queries.GetPublicSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Storefront-facing settings, readable without a login.
/// <para>
/// Separate from <c>Admin.SettingsController</c> rather than an extra action on it:
/// that controller is <c>[Authorize(Roles = Admin)]</c> at the class level and returns
/// whole <c>Setting</c> rows, and the settings table holds integration credentials.
/// Keeping the public read in its own controller means the anonymous path can never
/// widen by someone adding a field to the admin DTO.
/// </para>
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/settings")]
public sealed class PublicSettingsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns the storefront's settings — template choice and contact channels.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Key/value pairs for the allow-listed keys that have a value.</returns>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicSettingDto>>> GetPublicAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<PublicSettingDto>>(new GetPublicSettingsQuery(), ct);
        return Ok(result);
    }
}
