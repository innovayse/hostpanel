using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetSettings;

/// <summary>
/// Handles <see cref="GetSettingsQuery"/> by returning the configuration settings of the general catalogue.
/// </summary>
public sealed class GetSettingsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetSettingsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the catalogue settings ordered by key.
    /// </summary>
    /// <param name="query">The get settings query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with every configured catalogue setting; integration and deployment rows are not listed. This use case refuses nothing.</returns>
    public async Task<Result<IReadOnlyList<SettingDto>>> HandleAsync(GetSettingsQuery query, CancellationToken ct)
    {
        // Filtered in the database: the catalogue is a closed set of literals, so a row outside it
        // (an integration credential among them) is never loaded, let alone mapped.
        var keys = GeneralSettingKeys.All.ToList();
        var settings = await _context.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .OrderBy(s => s.Key)
            .ToListAsync(ct);
        IReadOnlyList<SettingDto> dtos = settings.Select(s => s.ToDto()).ToList();

        return Result<IReadOnlyList<SettingDto>>.Success(dtos);
    }
}
