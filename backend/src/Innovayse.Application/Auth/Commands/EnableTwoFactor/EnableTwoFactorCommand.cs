namespace Innovayse.Application.Auth.Commands.EnableTwoFactor;

/// <summary>Switches two-factor authentication on, once a code has proved the enrolment.</summary>
/// <remarks>
/// Whose account is not on the command at all. The handler asks the credential, so there is
/// no field a caller could set to act on somebody else's account.
/// </remarks>
/// <param name="Code">The six digits the authenticator app currently shows.</param>
public record EnableTwoFactorCommand(string Code);
