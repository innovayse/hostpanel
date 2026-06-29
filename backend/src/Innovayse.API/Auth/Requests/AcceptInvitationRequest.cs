namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body for accepting a client invitation.</summary>
/// <param name="Token">The invitation token from the invite URL.</param>
public record AcceptInvitationRequest(string Token);
