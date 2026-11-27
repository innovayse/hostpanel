using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;

/// <summary>
/// Initiates an incoming domain transfer via the registrar provider and persists the resulting aggregate.
/// Creates the domain in <c>PendingTransfer</c> status, then transitions it to <c>Active</c> once the
/// registrar confirms.
/// </summary>
public sealed class TransferDomainHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the new aggregate is persisted through.</summary>
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
    /// <param name="context">The data context the new aggregate is persisted through.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="payerCurrency">Decides the client's billing currency when the command names none.</param>
    /// <param name="contacts">Builds the registrant contact from the client's details.</param>
    /// <param name="fieldsValidation">Checks the registry's extra values against the fields the registrar plugin declares.</param>
    public TransferDomainHandler(
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
    /// Handles <see cref="TransferDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The transfer domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created domain ID, <c>CLIENT_NOT_FOUND</c> when the id names no client, or
    /// <c>DOMAIN_TRANSFER_UPSTREAM_FAILED</c> (or the registrar's own reason) when the registrar
    /// rejects the transfer request.
    /// </returns>
    public async Task<Result<int>> HandleAsync(TransferDomainCommand cmd, CancellationToken ct)
    {
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
            PluginDomainOperations.Transfer, cmd.ExtendedAttributes, ct);
        if (fields.IsFailure)
        {
            return Result<int>.FailureFrom(fields);
        }

        var extendedAttributes = DomainExtendedAttributes.From(cmd.ExtendedAttributes);
        var request = new DomainTransferOrder(
            cmd.DomainName, cmd.EppCode, cmd.WhoisPrivacy, contact.Value!, extendedAttributes.Values);
        var result = await registrar.Provider.TransferAsync(request, ct);

        if (!result.Success)
        {
            return Result.Failure<int>(
                $"Registrar rejected transfer of '{cmd.DomainName}': {result.ErrorCode}",
                result.ToFailureCode(HostpanelErrorCodes.DOMAIN_TRANSFER_UPSTREAM_FAILED));
        }

        var now = _clock.UtcNow;

        // The amounts are in the paid order's currency; a staff transfer without one bills in the client's.
        var currency = cmd.PriceCurrency ?? (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var domain = RegisteredDomain.CreateTransfer(
            cmd.ClientId, cmd.DomainName, now, currency,
            firstPaymentAmount: cmd.FirstPaymentAmount,
            recurringAmount: cmd.RecurringAmount,
            paymentMethod: cmd.PaymentMethod,
            registrationPeriod: cmd.Years,
            registrarModule: registrar.Module,
            orderId: cmd.OrderId,
            extendedAttributes: extendedAttributes);

        // When the registrar requires polling, leave in PendingTransfer.
        if (!result.RequiresPolling)
        {
            var expiresAt = result.ExpiresAt ?? now.AddYears(cmd.Years);
            domain.ActivateTransfer(result.RegistrarRef!, expiresAt, now);
        }

        _context.Domains.Add(domain);
        await _context.SaveChangesAsync(ct);
        return Result.Success(domain.Id);
    }
}
