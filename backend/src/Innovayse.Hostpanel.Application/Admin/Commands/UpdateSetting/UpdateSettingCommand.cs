using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UpdateSetting;

/// <summary>Command that updates the value of an existing configuration setting.</summary>
/// <param name="Id">
/// The setting's primary key; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> rather than being treated as
/// a required member, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Value">
/// The new value to assign. Absent or empty means the empty string, which only a key that seeds
/// empty accepts.
/// </param>
public sealed record UpdateSettingCommand(int Id, string? Value) : IDemoRestricted;
