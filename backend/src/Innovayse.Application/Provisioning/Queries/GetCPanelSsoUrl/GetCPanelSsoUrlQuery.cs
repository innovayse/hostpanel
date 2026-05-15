namespace Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;

/// <summary>Query to generate a single-sign-on URL for accessing the cPanel control panel.</summary>
/// <param name="ServiceId">The client service identifier.</param>
public record GetCPanelSsoUrlQuery(int ServiceId);
