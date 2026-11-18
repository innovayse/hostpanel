namespace Innovayse.Hostpanel.Application.Products.Queries.CheckProductLimits;

/// <summary>
/// Asks whether new services of a set of products fit inside each product's cap on live services and its
/// one-per-client rule. Every service about to be created is one entry, so a product named twice asks for two places.
/// </summary>
/// <param name="ProductIds">The products of the services about to be created, one entry per service.</param>
/// <param name="ClientId">The client the services are for, or null for a guest, who holds nothing yet.</param>
public sealed record CheckProductLimitsQuery(IReadOnlyList<int> ProductIds, int? ClientId = null);
