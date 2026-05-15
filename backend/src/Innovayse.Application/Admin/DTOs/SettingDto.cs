namespace Innovayse.Application.Admin.DTOs;

/// <summary>
/// Data transfer object representing a system configuration setting.
/// </summary>
/// <param name="Id">The setting's primary key.</param>
/// <param name="Key">The unique configuration key.</param>
/// <param name="Value">The current configuration value.</param>
/// <param name="Description">Optional human-readable description of this setting.</param>
public record SettingDto(int Id, string Key, string Value, string? Description);
