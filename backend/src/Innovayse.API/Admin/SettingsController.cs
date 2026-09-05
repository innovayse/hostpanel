namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Commands.UpdateSetting;
using Innovayse.Application.Admin.Commands.UploadBrandingImage;
using Innovayse.Application.Admin.Common;
using Innovayse.Application.Admin.Queries.GetSetting;
using Innovayse.Application.Admin.Queries.GetSettings;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing system configuration settings.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = Roles.Admin)]
public sealed class SettingsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all configuration settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all settings.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> GetAllAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<SettingDto>>(new GetSettingsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single setting by ID.</summary>
    /// <param name="id">Setting primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Setting DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SettingDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<SettingDto>(new GetSettingQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Updates the value of an existing setting.</summary>
    /// <param name="id">Setting primary key.</param>
    /// <param name="request">Request body containing the new value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateSettingCommand(id, request.Value), ct);
        return NoContent();
    }

    /// <summary>
    /// Uploads a storefront branding image and returns the URL to save into the setting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controller binds, caps and delegates. Deciding what the bytes are, rendering the icon
    /// set and writing the files all sit behind <c>UploadBrandingImageCommand</c>: the previous
    /// version of this action did the file I/O here and trusted the multipart part's own
    /// <c>Content-Type</c> header as its only check, which is a claim written by the caller.
    /// </para>
    /// <para>
    /// <see cref="RequestSizeLimitAttribute"/> is the ceiling the request never gets past, and it
    /// is deliberately larger than the configured one. This is the framework refusing to buffer an
    /// enormous body at all; <c>Branding:MaxBytes</c> is the operator-facing limit, and it answers
    /// with a readable refusal rather than a connection reset.
    /// </para>
    /// </remarks>
    /// <param name="kind">Which branding image this is: <c>logo</c> or <c>favicon</c>.</param>
    /// <param name="file">The image file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The primary URL, plus every file generated from the upload.</returns>
    [HttpPost("branding/{kind}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(BrandingUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BrandingUploadResultDto>> UploadBrandingImageAsync(
        string kind,
        IFormFile file,
        CancellationToken ct)
    {
        // IsDefined as well as TryParse: TryParse also accepts a numeric string and returns a
        // value that is not one of the named members, so "/branding/7" would otherwise reach the
        // storage layer and become a directory called "7" -- caller-steered input in a path,
        // which is exactly what that layer documents as impossible.
        if (!Enum.TryParse<BrandingKind>(kind, ignoreCase: true, out var parsedKind)
            || !Enum.IsDefined(parsedKind))
        {
            return BadRequest(new { error = "kind must be 'logo' or 'favicon'." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided." });
        }

        // Buffered rather than streamed: the whole image is needed in memory to decode it, the
        // request is already capped above, and a MemoryStream keeps the temp-file handling that a
        // large IFormFile would otherwise trigger out of the picture.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var command = new UploadBrandingImageCommand(
            parsedKind,
            new BrandingSource(buffer.ToArray(), file.FileName));

        var result = await bus.InvokeAsync<BrandingUploadResultDto>(command, ct);

        return Ok(result);
    }
}
