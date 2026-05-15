namespace Innovayse.Application.Clients.Commands.UpdateUserPermissions;

/// <summary>Command to update a non-owner user's permissions on a client account.</summary>
/// <param name="ClientId">The client account primary key.</param>
/// <param name="UserId">The Identity user ID whose permissions to update.</param>
/// <param name="Permissions">New permissions as a bit-flags integer.</param>
public record UpdateUserPermissionsCommand(int ClientId, string UserId, int Permissions);
