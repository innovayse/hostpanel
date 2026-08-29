namespace Innovayse.Application.Admin.Users.Commands.SendPasswordReset;

/// <summary>
/// Command to send a user a link they can use to choose a new password (admin action).
/// </summary>
/// <param name="Id">The person's subject.</param>
public record SendPasswordResetCommand(string Id);
