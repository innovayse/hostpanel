namespace Innovayse.Application.Auth.Commands.StartTwoFactorSetup;

/// <summary>Begins enrolling an authenticator app for one account.</summary>
/// <param name="UserId">Whose account. Comes from the credential, never from the request body.</param>
public record StartTwoFactorSetupCommand(string UserId);
