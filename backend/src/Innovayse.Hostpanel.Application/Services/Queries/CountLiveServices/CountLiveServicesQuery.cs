namespace Innovayse.Hostpanel.Application.Services.Queries.CountLiveServices;

/// <summary>
/// Requests how many live (not terminated) services each of a set of products has, so the order flow and the
/// catalogue can apply a product's cap on live services without reading the Services tables.
/// </summary>
/// <param name="ProductIds">The products to count; a product with no live service is answered with 0.</param>
/// <param name="ClientId">Counts only this client's services when set; every client's when null.</param>
/// <param name="ExcludedOrderItemIds">
/// Order lines the caller already counts itself: a service created from one of them is left out, so a line being
/// fulfilled is not counted twice. Null or empty leaves none out.
/// </param>
public sealed record CountLiveServicesQuery(
    IReadOnlyList<int> ProductIds,
    int? ClientId = null,
    IReadOnlyList<int>? ExcludedOrderItemIds = null);
