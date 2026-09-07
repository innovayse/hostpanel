namespace Innovayse.Application.Admin.Common;

/// <summary>
/// Thrown when an upload exceeds the byte ceiling, or decodes to more pixels than this
/// deployment will rasterise.
/// </summary>
/// <remarks>
/// <para>
/// The pixel ceiling is the half that matters and the half the old upload did not have. A file
/// well under the 5&#160;MB limit can still be a decompression bomb: a highly compressible
/// 100&#160;000&#160;&#215;&#160;100&#160;000 PNG is a few hundred kilobytes on disk and forty
/// gigabytes once decoded, so a size check alone lets an authenticated admin take the API down
/// with one upload. Both ceilings therefore report through this one type.
/// </para>
/// </remarks>
/// <param name="reason">Which ceiling was hit, for the log. Never sent to the caller.</param>
public sealed class BrandingImageTooLargeException(string reason) : Exception(PublicMessage)
{
    /// <summary>Machine-readable code sent as the <c>code</c> field of the error body.</summary>
    public const string Code = "BRANDING_IMAGE_TOO_LARGE";

    /// <summary>Key of the sentence in <c>ValidationMessages.resx</c>.</summary>
    public const string MessageKey = "BrandingImageTooLarge";

    /// <summary>The English sentence, carried by <see cref="System.Exception.Message"/>.</summary>
    public const string PublicMessage =
        "That image is too large. Upload a file under 5 MB and no more than 8000 pixels on a side.";

    /// <summary>Which ceiling was exceeded, for the log line beside the refusal.</summary>
    public string Reason { get; } = reason;
}
