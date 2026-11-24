using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// An active, auto-renewing service whose next renewal falls inside a window (or a converted trial awaiting
/// its first payment), with what billing needs to price and date its next period. Read by the billing
/// module, which never reads services itself.
/// </summary>
/// <param name="Id">The service.</param>
/// <param name="ClientId">The client who owns it.</param>
/// <param name="ProductId">The product it was ordered from, for the invoice line's name.</param>
/// <param name="BillingCycle">The billing cycle of the service.</param>
/// <param name="AnchorDay">
/// The day of month monthly periods keep: the day the service was created on, or for a converted trial the
/// day its paid period starts.
/// </param>
/// <param name="NextRenewalAt">Start of the next period to bill.</param>
/// <param name="RecurringAmount">The recurring price of one period.</param>
/// <param name="AutoTerminateEndOfCycle">Whether the service is set to terminate when its period ends.</param>
/// <param name="HasOpenCancellation">Whether an open cancellation request covers the service.</param>
public sealed record RenewableServiceDto(
    int Id,
    int ClientId,
    int ProductId,
    BillingCycle BillingCycle,
    int AnchorDay,
    DateTimeOffset NextRenewalAt,
    decimal RecurringAmount,
    bool AutoTerminateEndOfCycle,
    bool HasOpenCancellation)
{
    /// <summary>
    /// Whether the service is to end at the end of its period (set to terminate then, or an open cancellation request
    /// covers it), so its next period is not billed.
    /// </summary>
    public bool EndsAtPeriodEnd => AutoTerminateEndOfCycle || HasOpenCancellation;
}
