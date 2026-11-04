using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
/// Handles <see cref="RemovePaymentMethodCommand"/>.
/// </summary>
public sealed class RemovePaymentMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Resolves the caller's client record.</summary>
    private readonly IClientAccess _access;

    /// <summary>The clock the withdrawal's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="access">Resolves the caller's client record.</param>
    /// <param name="clock">The clock the withdrawal's moment is read from.</param>
    public RemovePaymentMethodHandler(IHostpanelDbContext context, StoredCredentialGateway gateway, IClientAccess access, IClock clock)
    {
        _context = context;
        _gateway = gateway;
        _access = access;
        _clock = clock;
    }

    /// <summary>Removes one saved payment method of the caller and ends the agreement made for it.</summary>
    /// <param name="cmd">The remove command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the reason the method was not removed.</returns>
    public async Task<Result> HandleAsync(RemovePaymentMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value;
        var customerRef = await _context.FindGatewayCustomerRefAsync(clientId, cmd.GatewayModuleId, ct);
        if (customerRef is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        }

        // Removal takes a bare method reference, so without this check a client could remove a method that belongs
        // to somebody else's account just by knowing (or guessing) its reference.
        var methods = await _gateway.ListMethodsAsync(cmd.GatewayModuleId, customerRef, ct);
        if (methods.IsFailure)
        {
            return Result.FailureCode(methods.ErrorCode!);
        }

        if (methods.Value!.All(m => m.MethodRef != cmd.PaymentMethodId))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        var removed = await _gateway.RemoveMethodAsync(cmd.GatewayModuleId, customerRef, cmd.PaymentMethodId, ct);
        if (removed.IsFailure)
        {
            return removed;
        }

        // A card that is gone is never charged: the agreement made for exactly this card at this module ends with it.
        // Other cards, other modules and other clients are left alone.
        var agreements = await _context.CardChargeConsents
            .Where(c => c.ClientId == clientId
                        && c.GatewayModuleId == cmd.GatewayModuleId
                        && c.GatewayMethodRef == cmd.PaymentMethodId
                        && c.Status != CardConsentStatus.Revoked)
            .ToListAsync(ct);
        foreach (var agreement in agreements)
        {
            agreement.Revoke(CardConsentRevocationReason.CardRemoved, _clock.UtcNow);
        }

        if (agreements.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
