namespace Innovayse.Domain.Orders.Interfaces;

/// <summary>Repository abstraction for <see cref="Order"/> aggregates.</summary>
public interface IOrderRepository
{
    /// <summary>
    /// Finds an order by its primary key, including items.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order if found, otherwise null.</returns>
    Task<Order?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Adds a new order to the repository.
    /// </summary>
    /// <param name="order">The order to persist.</param>
    void Add(Order order);

    /// <summary>
    /// Removes an order from the repository.
    /// </summary>
    /// <param name="order">The order to remove.</param>
    void Remove(Order order);

    /// <summary>
    /// Lists orders with optional status filtering and pagination.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="statusFilter">Optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged order list and total count.</returns>
    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        OrderStatus? statusFilter,
        CancellationToken ct);

    /// <summary>
    /// Returns the next sequential order number to use.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The next order number integer (e.g. 1, 2, 3…).</returns>
    Task<int> GetNextOrderNumberAsync(CancellationToken ct);

    /// <summary>Finds an order by its order number string.</summary>
    Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct);
}
