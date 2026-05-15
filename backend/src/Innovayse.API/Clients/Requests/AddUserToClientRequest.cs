namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for adding a user to a client account.</summary>
/// <param name="UserId">Identity user ID to link.</param>
/// <param name="Permissions">Granted permissions as a bit-flags integer.</param>
public record AddUserToClientRequest(string UserId, int Permissions);
