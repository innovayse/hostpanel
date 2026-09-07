namespace Innovayse.Application.Admin.Common;

/// <summary>
/// Thrown when uploaded branding bytes are not an image this deployment will store.
/// </summary>
/// <remarks>
/// <para>
/// One exception covers "not an image at all", "an image format we do not accept here" and "an
/// image whose declared type and real bytes disagree", on purpose. The three are the same event
/// from the operator's side -- this file cannot be used -- and separating them in the response
/// would tell an attacker probing the endpoint exactly which check they tripped.
/// </para>
/// <para>
/// The specific cause is written to the log instead, where the operator debugging their own
/// upload can reach it and a prober cannot.
/// </para>
/// </remarks>
/// <param name="reason">
/// Why the bytes were refused. Written to the log; never sent to the caller.
/// </param>
public sealed class InvalidBrandingImageException(string reason) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent as the <c>code</c> field of the error body. SCREAMING_SNAKE,
    /// like every other code on this platform. Part of the wire contract -- do not reword.
    /// </summary>
    public const string Code = "BRANDING_IMAGE_INVALID";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    public const string MessageKey = "BrandingImageInvalid";

    /// <summary>The English sentence, carried by <see cref="System.Exception.Message"/>.</summary>
    public const string PublicMessage =
        "That file could not be read as an image. Upload a PNG, JPEG, WebP, GIF or BMP.";

    /// <summary>
    /// The precise reason the bytes were refused, for the log line that accompanies the refusal.
    /// </summary>
    public string Reason { get; } = reason;
}
