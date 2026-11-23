using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetDomainContact;

/// <summary>
/// Reads the registrant contact the registrar currently holds for a domain, so a form that edits it
/// starts from the real record rather than from a guess.
/// </summary>
public sealed class GetDomainContactHandler
{
    /// <summary>The data context the domain aggregate is read through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain aggregate is read through.</param>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    public GetDomainContactHandler(IHostpanelDbContext context, RegistrarProviderResolver registrars)
    {
        _context = context;
        _registrars = registrars;
    }

    /// <summary>Handles <see cref="GetDomainContactQuery"/>.</summary>
    /// <param name="query">The get domain contact query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The registrant contact, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DOMAIN_CONTACT_NOT_FOUND</c> when the registrar holds no contact record for the name.
    /// </returns>
    public async Task<Result<DomainContactDto>> HandleAsync(GetDomainContactQuery query, CancellationToken ct)
    {
        var domain = await _context.Domains.FirstOrDefaultAsync(x => x.Id == query.DomainId, ct);
        if (domain is null)
        {
            return Result.Failure<DomainContactDto>(
                $"Domain {query.DomainId} not found.", HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var contact = await registrar.GetContactDetailsAsync(domain.ToRef(), ct);
        if (contact is null)
        {
            return Result.Failure<DomainContactDto>(
                $"Registrar holds no contact record for domain '{domain.Name}'.",
                HostpanelErrorCodes.DOMAIN_CONTACT_NOT_FOUND);
        }

        return Result.Success(new DomainContactDto(
            contact.FirstName, contact.LastName, contact.Organization, contact.Email, contact.Phone,
            contact.Address1, contact.Address2, contact.City, contact.State, contact.PostalCode,
            contact.Country));
    }
}
