namespace Innovayse.Application.Admin.Commands.UploadBrandingImage;

using Innovayse.Application.Admin.Common;
using Innovayse.Application.Admin.Interfaces;

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
/// <param name="processor">Validates the bytes and renders the icon set.</param>
/// <param name="storage">Writes the rendered files and reports their URLs.</param>
public sealed class UploadBrandingImageHandler(
    IBrandingImageProcessor processor,
    IBrandingStorage storage)
{
    /// <summary>
    /// Renders and stores the upload.
    /// </summary>
    /// <param name="command">The upload command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The primary URL to save into the setting, and every file written.</returns>
    /// <exception cref="InvalidBrandingImageException">The bytes are not an acceptable image.</exception>
    /// <exception cref="BrandingImageTooLargeException">The image exceeds a size ceiling.</exception>
    public async Task<BrandingUploadResultDto> HandleAsync(
        UploadBrandingImageCommand command,
        CancellationToken ct)
    {
        var renditions = await processor.RenderAsync(command.Source, command.Kind, ct);
        var assets = await storage.SaveAsync(command.Kind, renditions, ct);

        // The processor's contract guarantees exactly one Primary, and the storage contract
        // guarantees the order is preserved -- so this is a lookup, not a search that might fail.
        var primary = assets.First(a => a.Role == BrandingRole.Primary);

        return new BrandingUploadResultDto(primary.Url, assets);
    }
}
