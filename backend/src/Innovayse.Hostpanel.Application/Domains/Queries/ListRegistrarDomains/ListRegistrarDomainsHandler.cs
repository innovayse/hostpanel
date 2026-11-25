using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListRegistrarDomains;

/// <summary>
/// Handles <see cref="ListRegistrarDomainsQuery"/>: asks the registrar plugin for one page of the domains its account holds
/// and marks each one the panel already stores, whichever client owns it.
/// </summary>
public sealed class ListRegistrarDomainsHandler
{
    /// <summary>The data context the stored domains are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Builds the provider the chosen registrar module names.</summary>
    private readonly IRegistrarProviderFactory _factory;

    /// <summary>Tells which registrar plugins are installed, and how each spells its id.</summary>
    private readonly IRegistrarModuleCatalogue _modules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the stored domains are read from.</param>
    /// <param name="factory">Builds the provider the chosen registrar module names.</param>
    /// <param name="modules">Tells which registrar plugins are installed, and how each spells its id.</param>
    public ListRegistrarDomainsHandler(IHostpanelDbContext context, IRegistrarProviderFactory factory, IRegistrarModuleCatalogue modules)
    {
        _context = context;
        _factory = factory;
        _modules = modules;
    }

    /// <summary>Reads the page.</summary>
    /// <param name="query">The registrar and the cursor of the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The page, <c>REGISTRAR_NOT_CONFIGURED</c> when the module names no installed registrar plugin, or the <c>REGISTRAR_*</c>
    /// code the registrar refused with (<c>REGISTRAR_OPERATION_UNSUPPORTED</c> when it offers no domain list).
    /// </returns>
    public async Task<Result<RegistrarDomainListDto>> HandleAsync(ListRegistrarDomainsQuery query, CancellationToken ct)
    {
        if (query.RegistrarModule is null || _modules.FindId(query.RegistrarModule) is not { } module)
        {
            return Result<RegistrarDomainListDto>.FailureCode(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        }

        var provider = await _factory.GetProviderAsync(module, ct);
        var page = await provider.ListDomainsAsync(query.Cursor, ct);
        if (page.IsFailure)
        {
            return Result<RegistrarDomainListDto>.FailureCode(page.ErrorCode!);
        }

        // Stored names are lower case (RegisteredDomain.NormaliseName), so the registrar's spelling is brought to that form.
        var registrarPage = page.Value!;
        var listed = registrarPage.Domains;
        var names = listed.Select(d => RegisteredDomain.NormaliseName(d.Name)).ToList();
        var stored = await _context.Domains
            .AsNoTracking()
            .Where(d => names.Contains(d.Name))
            .Select(d => d.Name)
            .ToListAsync(ct);
        var inPanel = stored.ToHashSet();

        return Result.Success(new RegistrarDomainListDto(
            listed.Select(d => new RegistrarDomainDto(d.Name, d.ExpiresAt, inPanel.Contains(RegisteredDomain.NormaliseName(d.Name)))).ToList(),
            registrarPage.NextCursor));
    }
}
