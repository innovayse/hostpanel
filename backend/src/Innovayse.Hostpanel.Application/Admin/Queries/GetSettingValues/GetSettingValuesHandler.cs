using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;

/// <summary>Handles <see cref="GetSettingValuesQuery"/> by reading the named settings without tracking.</summary>
public sealed class GetSettingValuesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetSettingValuesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns the settings stored under the requested keys.</summary>
    /// <param name="query">The keys to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the settings that exist, ordered by key; a key outside the general catalogue is never read, so no secret can come back. This query refuses nothing.</returns>
    public async Task<Result<IReadOnlyList<SettingDto>>> HandleAsync(GetSettingValuesQuery query, CancellationToken ct)
    {
        var keys = query.Keys.Distinct(StringComparer.Ordinal).Where(GeneralSettingKeys.Contains).ToList();
        var settings = await _context.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

        IReadOnlyList<SettingDto> dtos = settings.Select(s => s.ToDto()).ToList();

        return Result<IReadOnlyList<SettingDto>>.Success(dtos);
    }
}
