namespace Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedServiceHostingResources;

/// <summary>
/// Records that the websites and databases plugins set up on a terminated service are gone: the hosting account they
/// lived in was removed with the service, so no server is called. Idempotent.
/// </summary>
/// <param name="ServiceId">The terminated service's primary key.</param>
public sealed record RemoveTerminatedServiceHostingResourcesCommand(int ServiceId);
