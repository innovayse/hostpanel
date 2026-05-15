namespace Innovayse.Application.Provisioning.Commands.UnsuspendService;

/// <summary>Command to re-activate a previously suspended hosting service on the provisioning provider.</summary>
/// <param name="ServiceId">The client service identifier to unsuspend.</param>
public record UnsuspendServiceCommand(int ServiceId);
