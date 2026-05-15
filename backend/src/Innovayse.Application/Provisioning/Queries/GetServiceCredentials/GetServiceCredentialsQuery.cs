namespace Innovayse.Application.Provisioning.Queries.GetServiceCredentials;

/// <summary>Query to retrieve the current login credentials for a provisioned hosting service.</summary>
/// <param name="ServiceId">The client service identifier.</param>
public record GetServiceCredentialsQuery(int ServiceId);
