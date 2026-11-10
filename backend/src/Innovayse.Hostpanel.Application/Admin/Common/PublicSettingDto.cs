namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// A single setting as exposed to unauthenticated callers.
/// <para>
/// Deliberately narrower than <see cref="SettingDto"/>: no primary key and no
/// description, because neither is useful to the storefront and this shape is served
/// without a login.
/// </para>
/// </summary>
/// <param name="Key">The configuration key.</param>
/// <param name="Value">The current value.</param>
public sealed record PublicSettingDto(string Key, string Value);
