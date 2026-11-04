using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetSetting;

/// <summary>
/// Handles <see cref="GetSettingQuery"/> by loading a single catalogue setting by ID.
/// </summary>
public sealed class GetSettingHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetSettingHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns a single setting DTO.
    /// </summary>
    /// <param name="query">The get setting query with the ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setting, or <c>SETTING_NOT_FOUND</c> when the id names no setting or a key outside the catalogue.</returns>
    public async Task<Result<SettingDto>> HandleAsync(GetSettingQuery query, CancellationToken ct)
    {
        var setting = await _context.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == query.Id, ct);
        if (setting is null || !GeneralSettingKeys.Contains(setting.Key))
        {
            return Result<SettingDto>.FailureCode(HostpanelErrorCodes.SETTING_NOT_FOUND);
        }

        return Result<SettingDto>.Success(setting.ToDto());
    }
}
