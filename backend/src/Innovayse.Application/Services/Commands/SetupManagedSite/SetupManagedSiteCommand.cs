namespace Innovayse.Application.Services.Commands.SetupManagedSite;

/// <summary>Command to set up a managed site service with TouchEstate theme deployment.</summary>
/// <param name="ServiceId">The client service ID to set up.</param>
/// <param name="Domain">The customer's domain name.</param>
/// <param name="TouchEstatePublicKey">Customer's TouchEstate public API key.</param>
/// <param name="TouchEstateSecretKey">Customer's TouchEstate secret API key.</param>
public record SetupManagedSiteCommand(
    int ServiceId,
    string Domain,
    string TouchEstatePublicKey,
    string TouchEstateSecretKey);
