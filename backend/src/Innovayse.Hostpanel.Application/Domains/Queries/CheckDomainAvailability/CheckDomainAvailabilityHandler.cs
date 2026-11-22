using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Common.Enums;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.CheckDomainAvailability;

/// <summary>
/// Checks whether a domain name is available for registration.
/// First checks the local database: if the domain is already registered through us it is not available.
/// Falls back to the registrar provider API for external availability.
/// </summary>
public sealed class CheckDomainAvailabilityHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context consulted first.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context consulted first.</param>
    public CheckDomainAvailabilityHandler(RegistrarProviderResolver registrars, IHostpanelDbContext context)
    {
        _registrars = registrars;
        _context = context;
    }

    /// <summary>
    /// Handles <see cref="CheckDomainAvailabilityQuery"/>.
    /// </summary>
    /// <param name="query">The check domain availability query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The trimmed name, whether it is available and the same outcome as a status. A TLD no registrar sells answers not
    /// available with <see cref="DomainAvailabilityStatus.TldNotSupported"/>; any other refusal of the registrar is its
    /// <c>REGISTRAR_*</c> code (an unreachable or refusing registrar is unknown, never "free" or "taken").
    /// </returns>
    public async Task<Result<DomainCheckResultDto>> HandleAsync(CheckDomainAvailabilityQuery query, CancellationToken ct)
    {
        var domain = query.Domain.Trim();

        // If the domain already exists in our system, it's not available
        var existing = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Name == RegisteredDomain.NormaliseName(domain), ct);
        var available = false;
        if (existing is null)
        {
            var registrar = await _registrars.ResolveForTldAsync(domain, ct);
            var checkedName = await registrar.Provider.CheckAvailabilityAsync(domain, ct);
            if (checkedName.ErrorCode == HostpanelErrorCodes.REGISTRAR_TLD_NOT_SUPPORTED)
            {
                return Result.Success(new DomainCheckResultDto(domain, false, DomainAvailabilityStatus.TldNotSupported));
            }

            if (checkedName.IsFailure)
            {
                return Result<DomainCheckResultDto>.FailureCode(checkedName.ErrorCode!);
            }

            available = checkedName.Value;
        }

        return Result.Success(new DomainCheckResultDto(
            domain,
            available,
            available ? DomainAvailabilityStatus.Available : DomainAvailabilityStatus.Taken));
    }
}
