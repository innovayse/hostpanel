namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelLoginDetails;

/// <summary>Reads how a person signs in to the panel of a service by hand, without a password.</summary>
/// <param name="ServiceId">Primary key of the service.</param>
public sealed record GetPanelLoginDetailsQuery(int ServiceId);
