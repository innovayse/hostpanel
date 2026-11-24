namespace Innovayse.Hostpanel.Application.Provisioning.Queries.ListServicePackages;

/// <summary>Lists the hosting packages the server of a provisioned service offers, so an admin can pick one to change to.</summary>
/// <param name="ServiceId">The client service identifier. Overlaid from the route.</param>
public sealed record ListServicePackagesQuery(int ServiceId);
