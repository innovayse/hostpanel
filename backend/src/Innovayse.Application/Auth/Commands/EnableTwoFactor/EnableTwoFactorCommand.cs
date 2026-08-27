namespace Innovayse.Application.Auth.Commands.EnableTwoFactor;

/// <summary>Switches two-factor authentication on, once a code has proved the enrolment.</summary>
/// <param name="UserId">Whose account. Comes from the credential, never from the request body.</param>
/// <param name="Code">The six digits the authenticator app currently shows.</param>
public record EnableTwoFactorCommand(string UserId, string Code);
