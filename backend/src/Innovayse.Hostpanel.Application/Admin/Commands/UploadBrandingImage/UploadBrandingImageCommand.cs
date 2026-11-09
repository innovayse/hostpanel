using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UploadBrandingImage;

/// <summary>
/// Command that stores a new storefront branding image and everything derived from it.
/// </summary>
/// <remarks>
/// Carries the stream rather than the <c>IFormFile</c> it arrived in: <c>IFormFile</c> is an
/// ASP.NET Core type, and a command that names it would drag the web framework into the
/// Application layer for no gain. The request-size limit on the action caps the body; the
/// handler reads the stream and hands the bytes to the image processor.
/// </remarks>
/// <param name="Kind">
/// Which branding image this is. The route binds it from the <see cref="BrandingKind"/> member
/// name, case-insensitive (<c>logo</c>, <c>logodark</c>, <c>logomark</c>, <c>favicon</c>); a numeric
/// segment also binds, so the validator still checks the value is a defined member.
/// </param>
/// <param name="Content">The uploaded stream; the caller owns and disposes it.</param>
/// <param name="Length">The declared length of the upload, in bytes.</param>
/// <param name="FileName">The untrusted client-supplied name, for diagnostics only.</param>
public sealed record UploadBrandingImageCommand(BrandingKind Kind, Stream Content, long Length, string FileName) : IDemoRestricted;
