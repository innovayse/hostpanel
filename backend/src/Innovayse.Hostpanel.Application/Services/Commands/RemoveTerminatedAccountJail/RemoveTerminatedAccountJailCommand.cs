namespace Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedAccountJail;

/// <summary>
/// Removes the SSH jail the server's key agent kept for a service whose hosting account the panel has just deleted.
/// Sent after a successful account termination; the agent itself refuses while the account still exists.
/// </summary>
/// <param name="ServiceId">The terminated service.</param>
public sealed record RemoveTerminatedAccountJailCommand(int ServiceId);
