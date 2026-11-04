using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Application.Admin.Extensions;

/// <summary>
/// Maps a <see cref="Setting"/> to <see cref="SettingDto"/>. The single-setting, the list and the
/// internal multi-key reads all go through this one mapping, so a field added to the DTO is added once.
/// </summary>
public static class SettingMappingExtensions
{
    /// <summary>Maps a setting of the general catalogue to its DTO.</summary>
    /// <param name="setting">The stored setting row; the caller has checked its key is in the catalogue.</param>
    /// <returns>The mapped DTO.</returns>
    public static SettingDto ToDto(this Setting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return new SettingDto(setting.Id, setting.Key, setting.Value, setting.Description);
    }
}
