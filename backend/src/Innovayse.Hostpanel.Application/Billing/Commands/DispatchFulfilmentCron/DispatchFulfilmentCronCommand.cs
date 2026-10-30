using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;

/// <summary>
/// Recurring job that delivers the due fulfilment records to their plugins. Self-reschedules at the
/// interval set in <c>ScheduledJobsOptions</c>, from the end of each pass.
/// </summary>
public sealed record DispatchFulfilmentCronCommand : IRecurringJob;
