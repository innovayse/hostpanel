using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>
/// Answers whether a service is to end at the end of its period, the one rule the trial use cases share: such a
/// trial is never converted, it is terminated. Billing reads the same rule from
/// <see cref="Innovayse.Hostpanel.Application.Services.Common.RenewableServiceDto.EndsAtPeriodEnd"/>.
/// </summary>
public static class ServicePeriodEndExtensions
{
    /// <summary>
    /// Whether the service is to end at the end of its period: the automation terminates it at the end of the cycle,
    /// or an open cancellation request covers it.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="service">The service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the service is set to terminate at the end of its cycle or a cancellation request is open.</returns>
    public static async Task<bool> EndsAtPeriodEndAsync(this IHostpanelDbContext context, ClientService service, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);

        return service.AutoTerminateEndOfCycle
               || await context.CancellationRequests.AsNoTracking()
                   .AnyAsync(r => r.ServiceId == service.Id && r.Status == CancellationStatus.Open, ct);
    }
}
