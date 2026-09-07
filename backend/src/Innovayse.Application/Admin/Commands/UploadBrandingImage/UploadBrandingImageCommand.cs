namespace Innovayse.Application.Admin.Commands.UploadBrandingImage;

using Innovayse.Application.Admin.Common;

/// <summary>
/// Command that stores a new storefront branding image and everything derived from it.
/// </summary>
/// <remarks>
/// Carries the bytes rather than the <c>IFormFile</c> they arrived in: <c>IFormFile</c> is an
/// ASP.NET Core type, and a command that names it would drag the web framework into the
/// Application layer for no gain. The controller reads the stream, applies the byte ceiling it
/// can enforce without decoding, and hands the result over.
/// </remarks>
/// <param name="Kind">Whether this replaces the logo or the favicon.</param>
/// <param name="Source">The uploaded bytes and their untrusted name.</param>
public record UploadBrandingImageCommand(BrandingKind Kind, BrandingSource Source);
