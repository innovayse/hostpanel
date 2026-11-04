using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyCardSave;

/// <summary>
/// Handles <see cref="CompleteMyCardSaveCommand"/>: completes the save session at the gateway module and, only
/// when the gateway says the method was saved and belongs to the client's account, makes the agreement active.
/// </summary>
public sealed class CompleteMyCardSaveHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Resolves the caller's client record.</summary>
    private readonly IClientAccess _access;

    /// <summary>Records the activity line of the saved card.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the activation moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="access">Resolves the caller's client record.</param>
    /// <param name="activity">Records the activity line of the saved card.</param>
    /// <param name="clock">The clock the activation moment is read from.</param>
    public CompleteMyCardSaveHandler(
        IHostpanelDbContext context, StoredCredentialGateway gateway, IClientAccess access, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _gateway = gateway;
        _access = access;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The complete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success when the method is saved and the agreement active (also when it already was); a failure when the session is unknown, was not completed, was withdrawn meanwhile or the gateway could not be reached.</returns>
    public async Task<Result> HandleAsync(CompleteMyCardSaveCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value;
        var consent = await _context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.ClientId == clientId && c.GatewayModuleId == cmd.GatewayModuleId && c.GatewaySessionId == cmd.SessionId, ct);
        if (consent is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
        }

        if (consent.Status == CardConsentStatus.Active)
        {
            return Result.Success();
        }

        if (consent.Status == CardConsentStatus.Revoked)
        {
            // Withdrawn before the gateway confirmed: the agreement does not come back.
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        var customerRef = await _context.FindGatewayCustomerRefAsync(clientId, cmd.GatewayModuleId, ct);
        if (customerRef is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
        }

        // What the browser says is never taken for granted: the gateway's own record decides.
        var completed = await _gateway.CompleteSaveMethodAsync(cmd.GatewayModuleId, cmd.SessionId, ct);
        if (completed.IsFailure)
        {
            return Result.FailureCode(completed.ErrorCode!);
        }

        if (!completed.Value!.Saved || completed.Value.Method is not { } saved)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        var methods = await _gateway.ListMethodsAsync(cmd.GatewayModuleId, customerRef, ct);
        if (methods.IsFailure)
        {
            return Result.FailureCode(methods.ErrorCode!);
        }

        if (methods.Value!.All(m => m.MethodRef != saved.MethodRef))
        {
            // The method is not on this client's account at the gateway, so the client has no right to agree for it.
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        if (!consent.Activate(saved.MethodRef, _clock.UtcNow))
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The agreement was withdrawn (or activated by a repeated call) between the read and the save: the
            // database kept the other write, and a withdrawal is final.
            _context.DiscardPendingChanges();
            var current = await _context.CardChargeConsents.AsNoTracking()
                .Where(c => c.Id == consent.Id).Select(c => c.Status).FirstAsync(ct);
            return current == CardConsentStatus.Active ? Result.Success() : Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(clientId, $"Card Saved For Automatic Renewal Charges - Payment Method ID: {saved.MethodRef}"), ct);

        // The first card becomes the default, so automatic charges have a card to use without a second step.
        if (!methods.Value!.Any(m => m.IsDefault))
        {
            await _gateway.SetDefaultMethodAsync(cmd.GatewayModuleId, customerRef, saved.MethodRef, ct);
        }

        return Result.Success();
    }
}
