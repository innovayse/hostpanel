namespace Innovayse.Application.Auth.Commands.Login;

/// <summary>
/// Command to authenticate a user with email and password.
/// On success returns <see cref="Innovayse.Application.Auth.DTOs.AuthWithRefreshDto"/> with tokens.
/// </summary>
/// <param name="Email">The user's registered email address.</param>
/// <param name="Password">The user's plain-text password.</param>
public record LoginCommand(string Email, string Password);
