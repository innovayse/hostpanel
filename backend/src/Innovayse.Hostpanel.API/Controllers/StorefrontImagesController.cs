using Innovayse.Hostpanel.Application.Admin.Commands.UploadBrandingImage;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The picture upload behind the storefront content editors (testimonial avatars, partner logos, project
/// images). It runs the branding pipeline, the bytes are decoded, re-encoded and stored by this process,
/// under the <see cref="BrandingKind.Content"/> kind, so what is served is never what was uploaded.
/// </summary>
[Route("api/admin/storefront")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class StorefrontImagesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public StorefrontImagesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Uploads a storefront content picture and returns the URL to save into the record.</summary>
    /// <remarks>
    /// <see cref="RequestSizeLimitAttribute"/> is the ceiling the request never gets past, deliberately above
    /// the operator-facing <c>Branding:MaxBytes</c>, which answers with a readable refusal.
    /// </remarks>
    /// <param name="file">The image file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the stored picture's URL; 400 when the image is not acceptable;
    /// 413 when the file is over the size limit; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit;
    /// 503 with <c>STORAGE_NOT_WRITABLE</c> when the server's upload folder is not writable.
    /// </returns>
    [HttpPost("upload-image")]
    [RequestSizeLimit(BrandingUploadLimits.RequestSizeLimitBytes)]
    [ProducesResponseType(typeof(BrandingUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadAsync(IFormFile file, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;
        var command = new UploadBrandingImageCommand(
            BrandingKind.Content, content, file?.Length ?? 0, file?.FileName ?? string.Empty);

        var outcome = await _bus.InvokeAsync<Result<BrandingUploadResultDto>>(command, ct);
        return HandleResult(outcome);
    }
}
