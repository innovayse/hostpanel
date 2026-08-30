namespace Innovayse.Application.Auth.Commands.CompleteSetup;

/// <summary>
/// Command for the first-run bootstrap: grants the Admin role to the authenticated caller.
/// </summary>
/// <remarks>
/// Carries no subject, for the same reason <c>AcceptInvitationCommand</c> does not: a field
/// naming who is being made Admin would let whoever holds the setup token make somebody else
/// Admin. The handler asks the credential.
/// </remarks>
/// <param name="SetupToken">
/// The token this installation printed to its log while setup was outstanding. Required under
/// <c>Auth:Mode=local</c>; ignored under <c>sso</c>, where no token is ever issued and the
/// accounts belong to the sign-on service.
/// </param>
public record CompleteSetupCommand(string? SetupToken);
