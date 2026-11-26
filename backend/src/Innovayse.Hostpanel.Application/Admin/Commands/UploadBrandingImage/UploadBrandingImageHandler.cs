using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UploadBrandingImage;

/// <summary>
/// Handles <see cref="UploadBrandingImageCommand"/> by rendering the upload into every file the
/// storefront needs and storing the set.
/// </summary>
/// <remarks>
/// The handler owns the sequence and nothing else: validate-and-render, then store, then report
/// which URL the setting should hold. Both steps are ports, so the use case reads the same
/// whether the files land on a mounted volume or in an object store, and neither the image
/// library nor the filesystem appears in this layer.
/// <para>
/// It deliberately does <b>not</b> write the setting row. The admin panel saves
/// <c>portal.logo</c> / <c>portal.favicon</c> through <c>UpdateSettingCommand</c> once the
/// operator confirms, which is what lets them upload, look at the preview and change their mind
/// without having already replaced the live storefront's logo.
/// </para>
/// </remarks>
public sealed class UploadBrandingImageHandler
{
    /// <summary>Validates the bytes and renders the icon set.</summary>
    private readonly IBrandingImageProcessor _processor;

    /// <summary>Writes the rendered files and reports their URLs.</summary>
    private readonly IBrandingStorage _storage;

    /// <summary>Initializes the handler.</summary>
    /// <param name="processor">Validates the bytes and renders the icon set.</param>
    /// <param name="storage">Writes the rendered files and reports their URLs.</param>
    public UploadBrandingImageHandler(IBrandingImageProcessor processor, IBrandingStorage storage)
    {
        _processor = processor;
        _storage = storage;
    }

    /// <summary>
    /// Renders and stores the upload.
    /// </summary>
    /// <param name="command">The upload command; the validator has already confirmed
    /// <see cref="UploadBrandingImageCommand.Kind"/> names a real <see cref="BrandingKind"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The primary URL to save into the setting and every file written, or
    /// <c>BRANDING_IMAGE_INVALID</c> when the bytes are not an acceptable image, or
    /// <c>BRANDING_IMAGE_TOO_LARGE</c> when the file is over the byte ceiling, or
    /// <c>BRANDING_IMAGE_DIMENSIONS_TOO_LARGE</c> (with its declared size) when the image is over a pixel ceiling, or
    /// <c>STORAGE_NOT_WRITABLE</c> when the upload folder refuses the write.
    /// </returns>
    public async Task<Result<BrandingUploadResultDto>> HandleAsync(
        UploadBrandingImageCommand command,
        CancellationToken ct)
    {
        // 400 (BRANDING_IMAGE_INVALID) or 413 (BRANDING_IMAGE_TOO_LARGE, BRANDING_IMAGE_DIMENSIONS_TOO_LARGE): the processor logs the
        // precise cause, which signature failed, which ceiling was hit, and keeps it out of
        // the body, so probing the endpoint teaches nothing about the checks it runs. The stream
        // is handed over unread: the processor owns the byte ceiling and reads no further than it.
        var source = new BrandingSource(command.Content, command.FileName);
        var rendered = await _processor.RenderAsync(source, command.Kind, ct);
        if (rendered.IsFailure)
        {
            return Result<BrandingUploadResultDto>.FailureFrom(rendered);
        }

        var stored = await _storage.SaveAsync(command.Kind, rendered.Value!, ct);
        if (stored.IsFailure)
        {
            return Result<BrandingUploadResultDto>.FailureFrom(stored);
        }

        // The processor's contract guarantees exactly one Primary, and the storage contract
        // guarantees the order is preserved, so this is a lookup, not a search that might fail.
        var assets = stored.Value!;
        var primary = assets.First(a => a.Role == BrandingRole.Primary);

        return Result<BrandingUploadResultDto>.Success(new BrandingUploadResultDto(primary.Url, assets));
    }
}
