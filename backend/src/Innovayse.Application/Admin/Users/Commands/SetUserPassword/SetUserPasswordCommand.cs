namespace Innovayse.Application.Admin.Users.Commands.SetUserPassword;

/// <summary>
/// Command to set a new password for a user directly, without the holder's involvement
/// (admin action).
/// </summary>
/// <param name="Id">The person's subject, taken from the route rather than the body.</param>
/// <param name="Password">The new password to set.</param>
public record SetUserPasswordCommand(string Id, string Password);
