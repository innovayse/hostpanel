using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetServiceRenewalInvoice;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Lets another module find the invoice that bills one period of a service (the conversion invoice of a trial)
/// through the bus, without reading the Billing tables itself.
/// </summary>
public static class ServiceRenewalInvoiceBusExtensions
{
    /// <summary>Finds the invoice that bills the period of a service starting at <paramref name="periodStart"/>.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="periodStart">The start of the billed period.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newest invoice that is not cancelled and bills the period, or <see langword="null"/> when there is none or
    /// the input failed validation.
    /// </returns>
    public static async Task<ServiceRenewalInvoiceDto?> GetServiceRenewalInvoiceAsync(
        this IMessageBus bus, int serviceId, DateTimeOffset periodStart, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<ServiceRenewalInvoiceDto?>>(
            new GetServiceRenewalInvoiceQuery(serviceId, periodStart), ct);

        return result.Value;
    }
}
