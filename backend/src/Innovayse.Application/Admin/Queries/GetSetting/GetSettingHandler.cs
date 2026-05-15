namespace Innovayse.Application.Admin.Queries.GetSetting;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetSettingQuery"/> by loading a single setting by ID.
/// </summary>
/// <param name="repo">Setting repository.</param>
public sealed class GetSettingHandler(ISettingRepository repo)
{
    /// <summary>
    /// Returns a single setting DTO.
    /// </summary>
    /// <param name="query">The get setting query with the ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setting DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the setting is not found.</exception>
    public async Task<SettingDto> HandleAsync(GetSettingQuery query, CancellationToken ct)
    {
        var setting = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Setting {query.Id} not found.");

        return new SettingDto(setting.Id, setting.Key, setting.Value, setting.Description);
    }
}
