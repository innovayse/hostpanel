namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Commands.UpdateSetting;
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
/// <param name="env">Web host environment for resolving file paths.</param>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = Roles.Admin)]
public sealed class SettingsController(IMessageBus bus, IWebHostEnvironment env) : ControllerBase
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
    /// Favicon allows a narrower set than the logo: browsers render an SVG or PNG tab
    /// icon directly, and .ico stays accepted for operators who already have one, but a
    /// JPEG/WebP/GIF favicon renders as an opaque square with no transparency —
    /// technically valid, visually wrong for what this field is for.
    /// </summary>
    private static readonly HashSet<string> _allowedLogoTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml"];

    private static readonly HashSet<string> _allowedFaviconTypes =
        ["image/png", "image/svg+xml", "image/x-icon", "image/vnd.microsoft.icon"];

    /// <summary>
    /// Uploads a storefront branding image (logo or favicon) and returns its URL.
    /// </summary>
    /// <param name="kind">Which branding image this is: <c>logo</c> or <c>favicon</c>.</param>
    /// <param name="file">The image file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative URL path to the uploaded image.</returns>
    [HttpPost("branding/{kind}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadBrandingImageAsync(string kind, IFormFile file, CancellationToken ct)
    {
        if (kind is not ("logo" or "favicon"))
        {
            return BadRequest(new { error = "kind must be 'logo' or 'favicon'." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided." });
        }

        var allowedTypes = kind == "favicon" ? _allowedFaviconTypes : _allowedLogoTypes;
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { error = $"Invalid file type for {kind}." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { error = "File too large. Maximum 5MB." });
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{kind}-{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(new { url = $"/uploads/branding/{fileName}" });
    }
}
