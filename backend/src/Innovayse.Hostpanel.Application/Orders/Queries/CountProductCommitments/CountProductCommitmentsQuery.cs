namespace Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;

/// <summary>
/// Requests how many services each of a set of products is committed to: live (not terminated) services, plus
/// lines of open orders that have not been fulfilled into a service yet. This is the number a product's cap on
/// live services and its one-per-client rule are checked against.
/// </summary>
/// <param name="ProductIds">The products to count; a product with nothing committed is answered with 0.</param>
/// <param name="ClientId">Counts only this client's services and orders when set; every client's when null.</param>
public sealed record CountProductCommitmentsQuery(IReadOnlyList<int> ProductIds, int? ClientId = null);
