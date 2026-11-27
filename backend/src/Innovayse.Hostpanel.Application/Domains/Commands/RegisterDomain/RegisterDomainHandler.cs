using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;

/// <summary>
/// Registers a new domain with the configured registrar provider and persists the resulting aggregate.
/// Calls <see cref="IRegistrarProvider.RegisterAsync"/> first, then creates and activates the domain aggregate.
/// </summary>
public sealed class RegisterDomainHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the new domain aggregate is checked against and persisted through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Decides the client's billing currency when the command names none.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Builds the registrant contact from the client's details.</summary>
    private readonly RegistrantContactReader _contacts;

    /// <summary>Checks the registry's extra values against the fields the registrar plugin declares.</summary>
    private readonly DomainFieldsValidation _fieldsValidation;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the new domain aggregate is checked against and persisted through.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="payerCurrency">Decides the client's billing currency when the command names none.</param>
    /// <param name="contacts">Builds the registrant contact from the client's details.</param>
    /// <param name="fieldsValidation">Checks the registry's extra values against the fields the registrar plugin declares.</param>
    public RegisterDomainHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        IClock clock,
        IPayerCurrencyResolver payerCurrency,
        RegistrantContactReader contacts,
        DomainFieldsValidation fieldsValidation)
    {
        _registrars = registrars;
        _context = context;
        _clock = clock;
        _payerCurrency = payerCurrency;
        _contacts = contacts;
        _fieldsValidation = fieldsValidation;
    }

    /// <summary>
    /// Handles <see cref="RegisterDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The register domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created domain ID, or <c>DOMAIN_ALREADY_REGISTERED</c> when the name is already
    /// registered through this system, <c>CLIENT_NOT_FOUND</c> when the id names no client, <c>DOMAIN_FIELD_REQUIRED</c>,
    /// <c>DOMAIN_FIELD_INVALID</c> or <c>DOMAIN_FIELD_UNKNOWN</c> when the registry's extra values do not match the fields the
    /// registrar plugin declares (nothing is sent to the registrar), or
    /// <c>DOMAIN_REGISTRATION_UPSTREAM_FAILED</c> (or the registrar's own reason) when the
    /// registrar rejects the request.
    /// </returns>
    public async Task<Result<int>> HandleAsync(RegisterDomainCommand cmd, CancellationToken ct)
    {
        // Safety net: the availability check should have caught this, but guard against duplicates
        var storedName = RegisteredDomain.NormaliseName(cmd.DomainName);
        var existing = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Name == storedName, ct);
        if (existing is not null)
        {
            return Result.Failure<int>(
                $"Domain '{cmd.DomainName}' is already registered in the system.",
                HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        }

        var contact = await _contacts.ReadAsync(cmd.ClientId, ct);
        if (contact.IsFailure)
        {
            return Result.Failure<int>(contact.Error!, contact.ErrorCode);
        }

        var registrar = await _registrars.ResolveForTldAsync(cmd.DomainName, ct);

        // Checked here as well as at checkout: this command is also the admin's, and a fulfilment retry can run it long after
        // the order was placed, against a registrar plugin whose manifest has changed since.
        var fields = await _fieldsValidation.ValidateAsync(
            cmd.DomainName, registrar.Module, DomainFieldsValidation.HolderOf(contact.Value!.Organization),
            PluginDomainOperations.Register, cmd.ExtendedAttributes, ct);
        if (fields.IsFailure)
        {
            return Result<int>.FailureFrom(fields);
        }

        var extendedAttributes = DomainExtendedAttributes.From(cmd.ExtendedAttributes);
        var request = new DomainRegistrationOrder(
            cmd.DomainName,
            cmd.Years,
            cmd.WhoisPrivacy,
            cmd.RegistrarAutoRenew,
            cmd.Nameserver1,
            cmd.Nameserver2,
            contact.Value!,
            extendedAttributes.Values);

        var result = await registrar.Provider.RegisterAsync(request, ct);

        if (!result.Success)
        {
            return Result.Failure<int>(
                $"Registrar rejected registration of '{cmd.DomainName}': {result.ErrorCode}",
                result.ToFailureCode(HostpanelErrorCodes.DOMAIN_REGISTRATION_UPSTREAM_FAILED));
        }

        var now = _clock.UtcNow;
        var expiresAt = result.ExpiresAt
            ?? now.AddYears(cmd.Years);

        // The amounts are in the paid order's currency; a staff registration without one bills in the client's.
        var currency = cmd.PriceCurrency ?? (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var domain = RegisteredDomain.Register(
            cmd.ClientId, cmd.DomainName, now, expiresAt, cmd.AutoRenew, cmd.WhoisPrivacy,
            recurringAmount: cmd.RecurringAmount,
            firstPaymentAmount: cmd.FirstPaymentAmount,
            priceCurrency: currency,
            registrationPeriod: cmd.Years,
            paymentMethod: cmd.PaymentMethod,
            registrarModule: registrar.Module,
            orderId: cmd.OrderId,
            extendedAttributes: extendedAttributes);

        // When the registrar requires polling (it processes the order asynchronously),
        // leave the domain in PendingRegistration: the SyncPendingDomainStatuses job will
        // activate it once the registrar confirms the domain is live.
        if (!result.RequiresPolling)
        {
            domain.Activate(result.RegistrarRef!, expiresAt, now);
        }

        _context.Domains.Add(domain);
        await _context.SaveChangesAsync(ct);
        return Result.Success(domain.Id);
    }
}
