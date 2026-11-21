using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetPublicLegalText;

/// <summary>
/// Handles <see cref="GetPublicLegalTextQuery"/> by returning the settings rows of one legal page that have a value.
/// <para>
/// The legal texts are not in <see cref="PortalSettingKeys.Public"/>: each is up to 12000 characters and the public
/// set rides in every page's payload. The keys here come from <see cref="PortalSettingKeys.LegalTexts"/> by page, an
/// allow-list like the public one, so the caller can never choose a key.
/// </para>
/// </summary>
public sealed class GetPublicLegalTextHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetPublicLegalTextHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns the rows of the page that currently have a value.</summary>
    /// <param name="query">The get public legal text query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the key and value of each language the operator wrote the page in; empty when it wrote none,
    /// or <c>SETTING_NOT_FOUND</c> when the page is not one of <see cref="PortalSettingKeys.LegalTexts"/>.
    /// </returns>
    public async Task<Result<IReadOnlyList<PublicSettingDto>>> HandleAsync(
        GetPublicLegalTextQuery query,
        CancellationToken ct)
    {
        if (!PortalSettingKeys.LegalTexts.TryGetValue(query.Page, out var keys))
        {
            return Result<IReadOnlyList<PublicSettingDto>>.FailureCode(HostpanelErrorCodes.SETTING_NOT_FOUND);
        }

        IReadOnlyList<PublicSettingDto> rows = await _context.Settings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key) && s.Value != string.Empty)
            .OrderBy(s => s.Key)
            .Select(s => new PublicSettingDto(s.Key, s.Value))
            .ToListAsync(ct);

        return Result<IReadOnlyList<PublicSettingDto>>.Success(rows);
    }
}
