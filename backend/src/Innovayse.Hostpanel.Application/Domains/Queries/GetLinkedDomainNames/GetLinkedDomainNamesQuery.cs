namespace Innovayse.Hostpanel.Application.Domains.Queries.GetLinkedDomainNames;

/// <summary>
/// Requests the registered domain linked to each named client service, so the Services module can
/// show it without reading the Domains table itself.
/// </summary>
/// <param name="ServiceIds">The client services to look up; a service with no linked domain is left out of the answer.</param>
public sealed record GetLinkedDomainNamesQuery(IReadOnlyList<int> ServiceIds);
