namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for updating a user's permissions on a client account.</summary>
/// <param name="Permissions">New permissions as a bit-flags integer.</param>
public record UpdateUserPermissionsRequest(int Permissions);
