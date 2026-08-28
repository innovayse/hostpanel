namespace Innovayse.Application.Auth.Commands.StartTwoFactorSetup;

/// <summary>Begins enrolling an authenticator app for one account.</summary>
/// <remarks>
/// Whose account is not on the command at all. The handler asks the credential, so there is
/// no field a caller could set to enrol a second factor onto somebody else's account.
/// </remarks>
public record StartTwoFactorSetupCommand();
