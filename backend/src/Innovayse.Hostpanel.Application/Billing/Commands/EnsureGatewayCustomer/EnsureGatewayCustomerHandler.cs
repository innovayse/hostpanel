using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.EnsureGatewayCustomer;

/// <summary>
/// Handles <see cref="EnsureGatewayCustomerCommand"/>: reads the client's account at the gateway module, or asks
/// the module's capability to open it and stores the reference.
/// </summary>
/// <remarks>
/// Two requests racing for a new client are covered twice. The capability answers the same reference for the
/// same client reference, so both get the same account at the gateway; and the unique index on the client and
/// the module lets only one insert win, the loser reading the winner's row.
/// </remarks>
public sealed class EnsureGatewayCustomerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Reads the client's name and e-mail address for the gateway account.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the link moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="bus">Reads the client's name and e-mail address for the gateway account.</param>
    /// <param name="clock">The clock the link moment is read from.</param>
    public EnsureGatewayCustomerHandler(IHostpanelDbContext context, StoredCredentialGateway gateway, IMessageBus bus, IClock clock)
    {
        _context = context;
        _gateway = gateway;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The ensure command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the gateway's customer reference; a failure when the module offers no saved methods or the gateway could not be reached.</returns>
    public async Task<Result<string>> HandleAsync(EnsureGatewayCustomerCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (await FindStoredAsync(cmd, ct) is { } stored)
        {
            return Result<string>.Success(stored);
        }

        var brief = await _bus.GetRequiredClientBriefAsync(cmd.ClientId, withEmail: true, ct);
        var opened = await _gateway.EnsureCustomerAsync(
            cmd.GatewayModuleId,
            new CredentialCustomerContext(cmd.ClientId.ToString(CultureInfo.InvariantCulture), brief.Email, brief.FullName),
            ct);
        if (opened.IsFailure)
        {
            // A request racing this one may have stored the account while the gateway was failing here.
            return await FindStoredAsync(cmd, ct) is { } winner
                ? Result<string>.Success(winner)
                : Result<string>.FailureCode(opened.ErrorCode!);
        }

        var customerRef = opened.Value!.CustomerRef;
        _context.GatewayCustomers.Add(GatewayCustomer.Create(cmd.ClientId, cmd.GatewayModuleId, customerRef, _clock.UtcNow));
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The unique index on the client and the module: another request stored the account first.
            _context.DiscardPendingChanges();
            return await FindStoredAsync(cmd, ct) is { } winner
                ? Result<string>.Success(winner)
                : Result<string>.FailureCode(HostpanelErrorCodes.CARD_GATEWAY_CUSTOMER_ALREADY_LINKED);
        }

        return Result<string>.Success(customerRef);
    }

    /// <summary>Reads the account already stored for the client and module.</summary>
    /// <param name="cmd">The ensure command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored reference, or <see langword="null"/> when none is stored yet.</returns>
    private Task<string?> FindStoredAsync(EnsureGatewayCustomerCommand cmd, CancellationToken ct) =>
        _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == cmd.ClientId && c.GatewayModuleId == cmd.GatewayModuleId)
            .Select(c => (string?)c.CustomerRef)
            .FirstOrDefaultAsync(ct);
}
