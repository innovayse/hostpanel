namespace Innovayse.Application.Admin.Common;

/// <summary>
/// The outcome of one branding upload: the value to store in the setting, plus every file that
/// was generated alongside it.
/// </summary>
/// <remarks>
/// <see cref="Url"/> stays the shape the admin panel already saves into <c>portal.logo</c> or
/// <c>portal.favicon</c>, so the setting row is unchanged and an operator who pastes a URL by
/// hand still works. <see cref="Assets"/> is what lets the storefront emit a complete icon set
/// without guessing at sibling filenames.
/// </remarks>
/// <param name="Url">The primary URL, saved into the setting.</param>
/// <param name="Assets">Every file written for this upload, primary included.</param>
public sealed record BrandingUploadResultDto(string Url, IReadOnlyList<BrandingAssetDto> Assets);
