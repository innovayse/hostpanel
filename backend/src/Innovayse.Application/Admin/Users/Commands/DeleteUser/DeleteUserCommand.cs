namespace Innovayse.Application.Admin.Users.Commands.DeleteUser;

/// <summary>
/// Command to delete an account. Client records that reference it are preserved as orphans.
/// </summary>
/// <param name="Id">The person's subject.</param>
public record DeleteUserCommand(string Id);
