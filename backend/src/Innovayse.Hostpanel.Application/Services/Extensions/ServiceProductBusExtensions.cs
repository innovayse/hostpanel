using Innovayse.Hostpanel.Application.Services.Queries.GetServiceProductIds;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>
/// The shape other modules ask the Services module which products a set of services belong to, so each dispatches
/// and unwraps the query the same way instead of reading the services tables.
/// </summary>
public static class ServiceProductBusExtensions
{
    /// <summary>The distinct products of the named services.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="serviceIds">The services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The product ids; none when no service exists or the input failed validation.</returns>
    public static async Task<IReadOnlyList<int>> GetServiceProductIdsAsync(
        this IMessageBus bus, IReadOnlyList<int> serviceIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new GetServiceProductIdsQuery(serviceIds), ct);

        return result.Value ?? [];
    }
}
