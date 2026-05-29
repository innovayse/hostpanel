namespace Innovayse.API.Services.Requests;

/// <summary>Request body for setting up a pending hosting service.</summary>
/// <param name="Domain">The domain name for the hosting account.</param>
/// <param name="Username">The desired control panel username.</param>
/// <param name="Password">The desired control panel password.</param>
public record SetupServiceRequest(string Domain, string Username, string Password);
