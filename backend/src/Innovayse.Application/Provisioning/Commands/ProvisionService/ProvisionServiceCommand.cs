namespace Innovayse.Application.Provisioning.Commands.ProvisionService;

/// <summary>Command to provision a hosting service on the configured provider.</summary>
/// <param name="ServiceId">The client service identifier to provision.</param>
public record ProvisionServiceCommand(int ServiceId);
