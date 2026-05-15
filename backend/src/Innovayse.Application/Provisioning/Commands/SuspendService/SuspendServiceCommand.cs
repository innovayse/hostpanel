namespace Innovayse.Application.Provisioning.Commands.SuspendService;

/// <summary>Command to suspend an active hosting service on the provisioning provider.</summary>
/// <param name="ServiceId">The client service identifier to suspend.</param>
/// <param name="Reason">Human-readable reason for the suspension (e.g. "Non-payment").</param>
public record SuspendServiceCommand(int ServiceId, string Reason);
