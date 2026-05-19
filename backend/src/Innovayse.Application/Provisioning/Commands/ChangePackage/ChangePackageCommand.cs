namespace Innovayse.Application.Provisioning.Commands.ChangePackage;

/// <summary>Command to change the hosting package on the provisioning server.</summary>
/// <param name="ServiceId">Client service primary key.</param>
/// <param name="NewPackage">The name of the new hosting package to assign.</param>
public record ChangePackageCommand(int ServiceId, string NewPackage);
