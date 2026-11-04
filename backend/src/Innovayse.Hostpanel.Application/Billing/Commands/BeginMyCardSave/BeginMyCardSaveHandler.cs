using Innovayse.Hostpanel.Application.Billing.Commands.EnsureGatewayCustomer;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginMyCardSave;

/// <summary>
/// Handles <see cref="BeginMyCardSaveCommand"/>: opens the client's account at the gateway module, starts a save
/// session there, and records the client's agreement as pending until <c>CompleteMyCardSave</c> sees the method
/// saved.
/// </summary>
public sealed class BeginMyCardSaveHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Resolves the caller's client record.</summary>
    private readonly IClientAccess _access;

    /// <summary>Dispatches the use cases this one builds on.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the agreement moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="access">Resolves the caller's client record.</param>
    /// <param name="bus">Dispatches the use cases this one builds on.</param>
    /// <param name="clock">The clock the agreement moment is read from.</param>
    public BeginMyCardSaveHandler(
        IHostpanelDbContext context, StoredCredentialGateway gateway, IClientAccess access, IMessageBus bus, IClock clock)
    {
        _context = context;
        _gateway = gateway;
        _access = access;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The begin command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the save session the browser completes; a failure when the account may not pay by card, the module offers no saved methods or the gateway could not be reached or answered a session the host cannot use.</returns>
    public async Task<Result<SavedMethodSessionDto>> HandleAsync(BeginMyCardSaveCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result<SavedMethodSessionDto>.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value;
        var mayTransact = await _bus.EnsureClientMayTransactAsync(clientId, ct);
        if (mayTransact.IsFailure)
        {
            return Result<SavedMethodSessionDto>.FailureCode(mayTransact.ErrorCode!);
        }

        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(clientId, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<SavedMethodSessionDto>.FailureCode(mayPayByCard.ErrorCode!);
        }

        var customer = await _bus.InvokeAsync<Result<string>>(new EnsureGatewayCustomerCommand(clientId, cmd.GatewayModuleId), ct);
        if (customer.IsFailure)
        {
            return Result<SavedMethodSessionDto>.FailureCode(customer.ErrorCode!);
        }

        var started = await _gateway.BeginSaveMethodAsync(
            cmd.GatewayModuleId, new SaveMethodContext(customer.Value!, cmd.ReturnUrl), ct);
        if (started.IsFailure)
        {
            return Result<SavedMethodSessionDto>.FailureCode(started.ErrorCode!);
        }

        var session = started.Value!;
        if (!IsUsable(session))
        {
            // A plugin that answers a session the host cannot hand to a browser is broken, not busy.
            return Result<SavedMethodSessionDto>.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
        }

        // The agreement waits as Pending until the gateway confirms, from its own record, that the method was saved.
        _context.CardChargeConsents.Add(CardChargeConsent.Create(
            clientId, cmd.GatewayModuleId, session.SessionId, cmd.Origin?.IpAddress, cmd.Origin?.UserAgent, _clock.UtcNow));
        await _context.SaveChangesAsync(ct);

        return Result.Success(new SavedMethodSessionDto(
            cmd.GatewayModuleId, session.SessionId, session.Mode, session.ClientToken, session.RedirectUrl));
    }

    /// <summary>Checks a session carries what its mode needs, so the browser is never handed a half-empty one.</summary>
    /// <param name="session">The session the capability answered.</param>
    /// <returns><see langword="true"/> when it has an id, a defined mode and the token or the web address that mode needs.</returns>
    private static bool IsUsable(SaveMethodSession session) =>
        !string.IsNullOrWhiteSpace(session.SessionId)
        && session.Mode switch
        {
            SaveMethodMode.EmbeddedToken => !string.IsNullOrWhiteSpace(session.ClientToken),

            // A rooted path such as "/pay" parses as an absolute file address on Linux, so the scheme is checked too:
            // the browser is only ever sent to a web address.
            SaveMethodMode.Redirect => Uri.TryCreate(session.RedirectUrl, UriKind.Absolute, out var redirect)
                && (redirect.Scheme == Uri.UriSchemeHttps || redirect.Scheme == Uri.UriSchemeHttp),
            _ => false,
        };
}
