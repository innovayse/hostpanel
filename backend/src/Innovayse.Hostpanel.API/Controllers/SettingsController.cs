using Innovayse.Hostpanel.Application.Admin.Commands.UpdateSetting;
using Innovayse.Hostpanel.Application.Admin.Commands.UploadBrandingImage;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSetting;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettings;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for managing system configuration settings and storefront branding.</summary>
[Route("api/admin/settings")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class SettingsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public SettingsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the settings of the general catalogue (portal, billing and plugin system); integration and deployment rows are not listed.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with every setting; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single catalogue setting by ID; any other row answers as if it did not exist.</summary>
    /// <param name="id">Setting primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the setting; 400 when a value is refused; 404 when the id names no catalogue setting;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SettingDto>>(new GetSettingQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Updates the value of an existing catalogue setting; integration fields are saved through the integration endpoints.</summary>
    /// <param name="id">Setting primary key; wins over any id in the body.</param>
    /// <param name="command">The new value; an empty one clears a key that seeds empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when the value does not match the key's vocabulary or shape, or the row is outside the
    /// general catalogue (<c>SETTING_NOT_EDITABLE</c>);
    /// 404 when the id names no setting; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateSettingCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Uploads a storefront branding image and returns the URL to save into the setting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controller binds, caps and delegates. Deciding what the bytes are, rendering the icon
    /// set and writing the files all sit behind <c>UploadBrandingImageCommand</c>.
    /// </para>
    /// <para>
    /// <see cref="RequestSizeLimitAttribute"/> is the ceiling the request never gets past, and it
    /// is deliberately larger than the configured one. This is the framework refusing to buffer an
    /// enormous body at all; <c>Branding:MaxBytes</c> is the operator-facing limit, and it answers
    /// with a readable refusal rather than a connection reset.
    /// </para>
    /// </remarks>
    /// <param name="kind">
    /// Which branding image this is: <c>logo</c>, <c>logodark</c>, <c>logomark</c> or
    /// <c>favicon</c>; the <see cref="BrandingKind"/> member name, case-insensitive.
    /// </param>
    /// <param name="file">The image file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the primary URL and every file generated from the upload;
    /// 400 when the kind or the image is not acceptable; 413 when the file is over the size limit;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("branding/{kind}")]
    [RequestSizeLimit(BrandingUploadLimits.RequestSizeLimitBytes)]
    [ProducesResponseType(typeof(BrandingUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadBrandingImageAsync(BrandingKind kind, IFormFile file, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;
        var command = new UploadBrandingImageCommand(
            kind, content, file?.Length ?? 0, file?.FileName ?? string.Empty);

        var outcome = await _bus.InvokeAsync<Result<BrandingUploadResultDto>>(command, ct);
        return HandleResult(outcome);
    }
}
