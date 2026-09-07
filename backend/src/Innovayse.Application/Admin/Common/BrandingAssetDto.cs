namespace Innovayse.Application.Admin.Common;

/// <summary>One stored branding file, as the admin panel and the storefront see it.</summary>
/// <param name="Url">Root-relative URL the file is served from.</param>
/// <param name="ContentType">The file's media type.</param>
/// <param name="PixelSize">Edge length for a square icon; <see langword="null"/> when not square-sized.</param>
/// <param name="Role">What the storefront links this file as.</param>
public sealed record BrandingAssetDto(string Url, string ContentType, int? PixelSize, BrandingRole Role);
