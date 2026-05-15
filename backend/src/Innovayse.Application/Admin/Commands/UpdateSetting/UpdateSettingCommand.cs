namespace Innovayse.Application.Admin.Commands.UpdateSetting;

/// <summary>Command that updates the value of an existing configuration setting.</summary>
/// <param name="Id">The setting's primary key.</param>
/// <param name="Value">The new value to assign.</param>
public record UpdateSettingCommand(int Id, string Value);
