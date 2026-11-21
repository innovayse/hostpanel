namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceProductIds;

/// <summary>Reads the products of a set of services, for the billing module.</summary>
/// <param name="ServiceIds">The services; an id that names no service adds nothing to the answer.</param>
public sealed record GetServiceProductIdsQuery(IReadOnlyList<int> ServiceIds);
