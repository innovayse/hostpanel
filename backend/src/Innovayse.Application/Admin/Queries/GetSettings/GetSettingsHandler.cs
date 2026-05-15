namespace Innovayse.Application.Admin.Queries.GetSettings;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetSettingsQuery"/> by returning all configuration settings.
/// </summary>
/// <param name="repo">Setting repository.</param>
public sealed class GetSettingsHandler(ISettingRepository repo)
{
    /// <summary>
    /// Returns all settings ordered by key.
    /// </summary>
    /// <param name="query">The get settings query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of all settings as DTOs.</returns>
    public async Task<IReadOnlyList<SettingDto>> HandleAsync(GetSettingsQuery query, CancellationToken ct)
    {
        var settings = await repo.ListAsync(ct);
        return settings
            .Select(s => new SettingDto(s.Id, s.Key, s.Value, s.Description))
            .ToList();
    }
}
