using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;

/// <summary>
/// One extra pass of the fulfilment dispatch, started right after a payment, refund or lapse commits so
/// a plugin hears about it in seconds instead of at the next beat. Nothing is scheduled: a separate command
/// from <see cref="DispatchFulfilmentCronCommand"/> so that kicking the dispatch can never start a second
/// chain beside the scheduled one. Best effort: the record is already saved, and the scheduled chain
/// delivers it if this pass never runs.
/// </summary>
public sealed record DispatchFulfilmentNowCommand : IRecurringJob;
