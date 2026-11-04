namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// Data transfer object representing a system configuration setting.
/// </summary>
/// <remarks>
/// Only a setting of the general catalogue (<c>GeneralSettingKeys</c>) is ever mapped to this DTO, and
/// no catalogue key is secret, so the value is always carried.
/// </remarks>
/// <param name="Id">The setting's primary key.</param>
/// <param name="Key">The unique configuration key.</param>
/// <param name="Value">The current configuration value.</param>
/// <param name="Description">Optional human-readable description of this setting.</param>
public sealed record SettingDto(int Id, string Key, string? Value, string? Description);
