using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.UpgradeMyTrial;

/// <summary>
/// Converts one of the calling client's own free trials into a paid service, refusing every service that is not theirs.
/// </summary>
/// <remarks>
/// Ownership is settled <b>before</b> <see cref="ConvertTrialCommand"/> is dispatched, which names no caller: a
/// stranger's id never reaches the conversion, so nothing of theirs is converted or invoiced.
/// </remarks>
public sealed class UpgradeMyTrialHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Resolves the account the caller acts for, owner or linked user.</summary>
    private readonly IClientAccess _access;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public UpgradeMyTrialHandler(IServiceOwnership ownership, IClientAccess access, IMessageBus bus)
    {
        _ownership = ownership;
        _access = access;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, <c>CLIENT_SUSPENDED</c> or
    /// <c>CLIENT_CLOSED</c> when the account may not transact, or the refusal of <see cref="ConvertTrialCommand"/>
    /// such as <c>SERVICE_NOT_IN_TRIAL</c> or <c>SERVICE_NOT_ACTIVE</c>.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(UpgradeMyTrialCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var owned = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (owned.IsFailure)
        {
            return owned;
        }

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result.FailureCode(clientId.ErrorCode!);
        }

        var mayTransact = await _bus.EnsureClientMayTransactAsync(clientId.Value, ct);
        if (mayTransact.IsFailure)
        {
            return mayTransact;
        }

        return await _bus.InvokeAsync<Result>(new ConvertTrialCommand(cmd.ServiceId), ct);
    }
}
