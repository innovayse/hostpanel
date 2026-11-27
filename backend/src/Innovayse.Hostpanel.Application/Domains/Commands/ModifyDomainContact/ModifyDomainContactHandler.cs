using Hostpanel.Sdk.Plugins.Enums;
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

    /// <summary>Checks the registry's extra values against the fields the registrar plugin declares.</summary>
    private readonly DomainFieldsValidation _fieldsValidation;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<ModifyDomainContactHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain aggregate is read through.</param>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="fieldsValidation">Checks the registry's extra values against the fields the registrar plugin declares.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public ModifyDomainContactHandler(
        IHostpanelDbContext context,
        RegistrarProviderResolver registrars,
        DomainFieldsValidation fieldsValidation,
        ILogger<ModifyDomainContactHandler> logger)
    {
        _context = context;
        _registrars = registrars;
        _fieldsValidation = fieldsValidation;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="ModifyDomainContactCommand"/>.
    /// </summary>
    /// <param name="cmd">The modify domain contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, <c>DOMAIN_FIELD_REQUIRED</c>,
    /// <c>DOMAIN_FIELD_INVALID</c> or <c>DOMAIN_FIELD_UNKNOWN</c> when the registry's extra values do not match the fields the
    /// registrar plugin declares, or <c>DOMAIN_CONTACT_UPSTREAM_FAILED</c> when the registrar rejects the contact update.
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
        var fields = await _fieldsValidation.ValidateAsync(
            domain.Name, domain.RegistrarModule, DomainFieldsValidation.HolderOf(contact.Organization),
            PluginDomainOperations.ContactChange, cmd.ExtendedAttributes, ct);
        if (fields.IsFailure)
        {
            return fields;
        }

        var extendedAttributes = DomainExtendedAttributes.From(cmd.ExtendedAttributes);
        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.ModifyContactDetailsAsync(
            new DomainContactChange(domain.ToRef(), contact, extendedAttributes.Values), ct);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar failed to modify contact details for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_CONTACT_UPSTREAM_FAILED));
        }

        // The registrar now holds these values, so the stored ones follow, and the contact form reads them back as they were sent.
        domain.ReplaceExtendedAttributes(extendedAttributes);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
