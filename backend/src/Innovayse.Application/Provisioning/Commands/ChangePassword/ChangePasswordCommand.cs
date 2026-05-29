namespace Innovayse.Application.Provisioning.Commands.ChangePassword;

/// <summary>Command to change the hosting account password on the provisioning server.</summary>
/// <param name="ServiceId">Client service primary key.</param>
/// <param name="NewPassword">The new password to set.</param>
public record ChangePasswordCommand(int ServiceId, string NewPassword);
