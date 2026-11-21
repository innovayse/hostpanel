using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetAllowedPaymentMethods;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductSummaries;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The shapes other modules ask the Products module's queries in, so each caller dispatches the
/// query the same way instead of reading the Products tables.
/// </summary>
public static class ProductBusExtensions
{
    /// <summary>Whether a product with this id exists.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="productId">The id to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the product exists.</returns>
    public static async Task<bool> ProductExistsAsync(this IMessageBus bus, int productId, CancellationToken ct)
    {
        var summaries = await SendAsync(bus, [productId], ct);

        return summaries.Count > 0;
    }

    /// <summary>
    /// The product that a row held by a restricting foreign key names (a client service, an order
    /// line): the database refuses to delete a product while such a row exists.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="productId">The product's id, read from the row.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="InvalidOperationException">The id names no product, so the database is broken.</exception>
    public static async Task<ProductSummaryDto> GetRequiredProductSummaryAsync(
        this IMessageBus bus, int productId, CancellationToken ct)
    {
        var summaries = await SendAsync(bus, [productId], ct);

        return summaries.FirstOrDefault() ?? throw MissingProduct(productId);
    }

    /// <summary>
    /// The products that rows held by a restricting foreign key name, for a list that shows a
    /// product name per row; see <see cref="GetRequiredProductSummaryAsync"/>.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="productIds">The product ids, read from the rows; duplicates are answered once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every requested product keyed by id.</returns>
    /// <exception cref="InvalidOperationException">An id names no product, so the database is broken.</exception>
    public static async Task<IReadOnlyDictionary<int, ProductSummaryDto>> GetRequiredProductSummariesByIdAsync(
        this IMessageBus bus, IReadOnlyList<int> productIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        var summaries = (await SendAsync(bus, productIds, ct)).ToDictionary(p => p.Id);
        foreach (var productId in productIds)
        {
            if (!summaries.ContainsKey(productId))
            {
                throw MissingProduct(productId);
            }
        }

        return summaries;
    }

    /// <summary>The payment methods the product groups of some products allow together.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="productIds">The products; an id that names no product is ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> when no group is restricted; otherwise the methods all restricted groups share,
    /// empty when they share none.
    /// </returns>
    public static async Task<IReadOnlyList<string>?> GetAllowedPaymentMethodsAsync(
        this IMessageBus bus, IReadOnlyList<int> productIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<string>?>>(new GetAllowedPaymentMethodsQuery(productIds), ct);

        return result.Value;
    }

    /// <summary>The failure for a product id that a restricting foreign key should have kept alive.</summary>
    /// <param name="productId">The product id that names no product.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException MissingProduct(int productId)
    {
        return new InvalidOperationException(
            $"Product {productId} does not exist, yet a row whose foreign key restricts deleting it names it.");
    }

    /// <summary>Dispatches <see cref="GetProductSummariesQuery"/> and unwraps its answer.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="productIds">The products to describe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The summaries of the products that exist, or none when an id failed validation.</returns>
    private static async Task<IReadOnlyList<ProductSummaryDto>> SendAsync(
        IMessageBus bus, IReadOnlyList<int> productIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(new GetProductSummariesQuery(productIds), ct);

        return result.Value ?? [];
    }
}
