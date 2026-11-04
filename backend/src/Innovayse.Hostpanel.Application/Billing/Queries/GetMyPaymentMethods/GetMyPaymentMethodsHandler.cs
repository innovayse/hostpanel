using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyPaymentMethods;

/// <summary>
/// Handles <see cref="GetMyPaymentMethodsQuery"/>: lists the payment methods the caller has saved, at every
/// gateway module they have an account with, and marks the ones they agreed may be charged automatically.
/// </summary>
public sealed class GetMyPaymentMethodsHandler
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
    public GetMyPaymentMethodsHandler(IHostpanelDbContext context, StoredCredentialGateway gateway, IClientAccess access)
    {
        _context = context;
        _gateway = gateway;
        _access = access;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it names no account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's saved methods (none when no module holds any), or the gateway failure that stopped the listing.</returns>
    public async Task<Result<IReadOnlyList<CardPaymentMethodDto>>> HandleAsync(GetMyPaymentMethodsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result<IReadOnlyList<CardPaymentMethodDto>>.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value;
        var accounts = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(g => g.ClientId == clientId)
            .Select(g => new { g.GatewayModuleId, g.CustomerRef })
            .ToListAsync(ct);
        var agreed = (await _context.CardChargeConsents
                .AsNoTracking()
                .Where(c => c.ClientId == clientId && c.Status == CardConsentStatus.Active)
                .Select(c => new { c.GatewayModuleId, c.GatewayMethodRef })
                .ToListAsync(ct))
            .Select(c => (c.GatewayModuleId, c.GatewayMethodRef))
            .ToHashSet();

        var methods = new List<CardPaymentMethodDto>();
        foreach (var account in accounts)
        {
            var listed = await _gateway.ListMethodsAsync(account.GatewayModuleId, account.CustomerRef, ct);
            if (listed.ErrorCode == HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE)
            {
                // The module was switched off or no longer offers saved methods: nothing can be listed from it.
                continue;
            }

            if (listed.IsFailure)
            {
                return Result<IReadOnlyList<CardPaymentMethodDto>>.FailureCode(listed.ErrorCode!);
            }

            methods.AddRange(listed.Value!.Select(m => new CardPaymentMethodDto(
                account.GatewayModuleId,
                m.MethodRef,
                m.Brand,
                m.Last4,
                m.ExpiryMonth,
                m.ExpiryYear,
                m.IsDefault,
                agreed.Contains((account.GatewayModuleId, m.MethodRef)))));
        }

        return Result.Success<IReadOnlyList<CardPaymentMethodDto>>(methods);
    }
}
