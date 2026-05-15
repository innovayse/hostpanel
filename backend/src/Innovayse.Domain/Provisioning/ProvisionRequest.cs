namespace Innovayse.Domain.Provisioning;

/// <summary>
/// Request to provision a new hosting service account on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="DomainName">Primary domain name to associate with the new account.</param>
/// <param name="Username">cPanel/control-panel username for the new account.</param>
/// <param name="Password">Initial password for the new account.</param>
/// <param name="Package">Hosting package name as configured on the provider server.</param>
public record ProvisionRequest(int ServiceId, string DomainName, string Username, string Password, string Package);
