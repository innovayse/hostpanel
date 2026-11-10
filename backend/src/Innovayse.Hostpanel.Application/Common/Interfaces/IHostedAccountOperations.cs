using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Suspends, unsuspends and terminates the hosting account behind a client service on the
/// panel of the server it lives on.
/// </summary>
/// <remarks>
/// One place for the server lookup, the provider call and the logging of a failure, which the
/// staff commands and the domain-event handlers all need. A command maps
/// <see cref="HostedAccountOutcome.Failed"/> to its own error code; an event handler only has the
/// log line this writes.
/// </remarks>
public interface IHostedAccountOperations
{
    /// <summary>Suspends the hosting account on its panel.</summary>
    /// <param name="input">The account to suspend and why.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the panel was told, had nothing to tell, or refused.</returns>
    public Task<HostedAccountOutcome> SuspendAsync(HostedAccountSuspension input, CancellationToken ct);

    /// <summary>Reactivates a suspended hosting account on its panel.</summary>
    /// <param name="input">The account to reactivate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the panel was told, had nothing to tell, or refused.</returns>
    public Task<HostedAccountOutcome> UnsuspendAsync(HostedAccountUnsuspension input, CancellationToken ct);

    /// <summary>Removes the hosting account from its panel.</summary>
    /// <param name="input">The account to remove and why.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the panel was told, had nothing to tell, or refused.</returns>
    public Task<HostedAccountOutcome> TerminateAsync(HostedAccountTermination input, CancellationToken ct);
}
