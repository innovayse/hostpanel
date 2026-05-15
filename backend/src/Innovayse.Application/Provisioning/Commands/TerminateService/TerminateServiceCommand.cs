namespace Innovayse.Application.Provisioning.Commands.TerminateService;

/// <summary>Command to permanently terminate a hosting service on the provisioning provider.</summary>
/// <param name="ServiceId">The client service identifier to terminate.</param>
/// <param name="Reason">Human-readable reason for the termination.</param>
public record TerminateServiceCommand(int ServiceId, string Reason);
