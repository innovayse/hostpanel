using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetRenewableServices;
using Innovayse.Hostpanel.Application.Services.Queries.GetServiceBillingStates;
using Innovayse.Hostpanel.Application.Services.Queries.GetTrialConversionService;
using Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspendedServices;
using Innovayse.Hostpanel.Application.Services.Queries.ListUndatedRenewingServices;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>
/// The shapes recurring billing asks the Services module's renewal queries in, so every caller
/// dispatches and unwraps them the same way instead of reading the services tables.
/// </summary>
public static class ServiceRenewalBusExtensions
{
    /// <summary>The active, auto-renewing services due on or before <paramref name="dueBy"/>.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientId">One client's services, or every client's when <see langword="null"/>.</param>
    /// <param name="dueBy">The end of the window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The due services, or none when the input failed validation.</returns>
    public static async Task<IReadOnlyList<RenewableServiceDto>> GetRenewableServicesAsync(
        this IMessageBus bus, int? clientId, DateTimeOffset dueBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<RenewableServiceDto>>>(new GetRenewableServicesQuery(clientId, dueBy), ct);

        return result.Value ?? [];
    }

    /// <summary>The converted trial awaiting the payment of its first paid period.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The service, or <see langword="null"/> when it is not an Active service whose trial is awaiting payment, or
    /// when the input failed validation.
    /// </returns>
    public static async Task<RenewableServiceDto?> GetTrialConversionServiceAsync(this IMessageBus bus, int serviceId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<RenewableServiceDto?>>(new GetTrialConversionServiceQuery(serviceId), ct);

        return result.Value;
    }

    /// <summary>The suspension-related state of the named services.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="serviceIds">The services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The states keyed by service id; an unknown id is absent.</returns>
    public static async Task<IReadOnlyDictionary<int, ServiceBillingStateDto>> GetServiceBillingStatesAsync(
        this IMessageBus bus, IReadOnlyList<int> serviceIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<ServiceBillingStateDto>>>(new GetServiceBillingStatesQuery(serviceIds), ct);

        return (result.Value ?? []).ToDictionary(s => s.Id);
    }

    /// <summary>The ids of the services suspended for an overdue invoice.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids.</returns>
    public static async Task<IReadOnlyList<int>> ListOverdueSuspendedServicesAsync(this IMessageBus bus, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListOverdueSuspendedServicesQuery(), ct);

        return result.Value ?? [];
    }

    /// <summary>The ids of the auto-renewing services on a renewable cycle that have no renewal date.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids.</returns>
    public static async Task<IReadOnlyList<int>> ListUndatedRenewingServicesAsync(this IMessageBus bus, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListUndatedRenewingServicesQuery(), ct);

        return result.Value ?? [];
    }
}
