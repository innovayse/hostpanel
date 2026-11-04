using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Handles <see cref="SetDefaultPaymentMethodCommand"/>.
/// </summary>
public sealed class SetDefaultPaymentMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Resolves the caller's client record.</summary>
    private readonly IClientAccess _access;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="access">Resolves the caller's client record.</param>
    public SetDefaultPaymentMethodHandler(IHostpanelDbContext context, StoredCredentialGateway gateway, IClientAccess access)
    {
        _context = context;
        _gateway = gateway;
        _access = access;
    }

    /// <summary>Makes one of the caller's saved payment methods the default.</summary>
    /// <param name="cmd">The set-default command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the reason the default was not changed.</returns>
    public async Task<Result> HandleAsync(SetDefaultPaymentMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var customerRef = await _context.FindGatewayCustomerRefAsync(caller.Value, cmd.GatewayModuleId!, ct);
        if (customerRef is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        }

        // Checking here first means a mismatched reference gets this handler's own error rather than an opaque
        // gateway refusal, and keeps the check in place whatever the gateway validates itself.
        var methods = await _gateway.ListMethodsAsync(cmd.GatewayModuleId!, customerRef, ct);
        if (methods.IsFailure)
        {
            return Result.FailureCode(methods.ErrorCode!);
        }

        if (methods.Value!.All(m => m.MethodRef != cmd.PaymentMethodId))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        return await _gateway.SetDefaultMethodAsync(cmd.GatewayModuleId!, customerRef, cmd.PaymentMethodId!, ct);
    }
}
