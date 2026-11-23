using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ModifyDomainContact;

/// <summary>
/// Sends updated WHOIS registrant contact details to the registrar for a domain.
/// </summary>
public sealed class ModifyDomainContactHandler
{
    /// <summary>The data context the domain aggregate is read through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<ModifyDomainContactHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain aggregate is read through.</param>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public ModifyDomainContactHandler(
        IHostpanelDbContext context,
        RegistrarProviderResolver registrars,
        ILogger<ModifyDomainContactHandler> logger)
    {
        _context = context;
        _registrars = registrars;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="ModifyDomainContactCommand"/>.
    /// </summary>
    /// <param name="cmd">The modify domain contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DOMAIN_CONTACT_UPSTREAM_FAILED</c> when the registrar rejects the contact update.
    /// </returns>
    public async Task<Result> HandleAsync(ModifyDomainContactCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is null)
        {
            // Contacts live only at the registrar; there is nothing to change for a domain that
            // was never placed at one, and reporting success would claim a change nobody made.
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        }

        var contact = new DomainContact(
            cmd.FirstName, cmd.LastName, cmd.Organization, cmd.Email, cmd.Phone,
            cmd.Address1, cmd.Address2, cmd.City, cmd.State, cmd.PostalCode, cmd.Country);
        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.ModifyContactDetailsAsync(new DomainContactChange(domain.ToRef(), contact), ct);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar failed to modify contact details for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_CONTACT_UPSTREAM_FAILED));
        }

        return Result.Success();
    }
}
