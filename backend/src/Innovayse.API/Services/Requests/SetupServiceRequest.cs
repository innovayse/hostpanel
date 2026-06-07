namespace Innovayse.API.Services.Requests;

/// <summary>Request body for setting up a pending hosting service.</summary>
/// <param name="Domain">The domain name for the hosting account.</param>
/// <param name="Username">The desired control panel username. Required for standard hosting products.</param>
/// <param name="Password">The desired control panel password. Required for standard hosting products.</param>
/// <param name="TouchEstatePublicKey">TouchEstate public API key. Required for ManagedSiteTouchestate products.</param>
/// <param name="TouchEstateSecretKey">TouchEstate secret API key. Required for ManagedSiteTouchestate products.</param>
public record SetupServiceRequest(
    string Domain,
    string? Username,
    string? Password,
    string? TouchEstatePublicKey,
    string? TouchEstateSecretKey);
