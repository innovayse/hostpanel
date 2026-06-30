namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for accepting a client invitation.</summary>
/// <param name="Token">The invitation token.</param>
/// <param name="Password">The password to set for the new account.</param>
public record AcceptInvitationRequest(string Token, string Password);
