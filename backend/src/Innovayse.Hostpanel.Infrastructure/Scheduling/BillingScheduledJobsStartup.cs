using Innovayse.Hostpanel.Application.Admin.Servers.Commands.SyncServerAccountCountsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ProcessBillableItemsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ProcessRenewalsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.SendRenewalRemindersCron;
using Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Provisioning.Commands.AutoTerminateSuspendedCron;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Innovayse.Hostpanel.Infrastructure.Scheduling;

/// <summary>
/// Hosted service that starts the billing jobs on application startup: renewal invoices, billable
/// items processing, renewal reminders, overdue checks, overdue suspensions, plus the server sync,
/// auto-terminate, gateway reconcile and fulfilment dispatch jobs. Their hours and intervals are the
/// <c>ScheduledJobsOptions</c> values the job handlers read too.
/// </summary>
/// <remarks>
/// <see cref="RecurringJobs.StartAsync{TCommand}"/> schedules each job for its next regular run, or
/// right now when the job missed its last one (the application was down or restarting during the run
/// hour). The handlers schedule every later run themselves.
/// </remarks>
public sealed class BillingScheduledJobsStartup : IHostedService
{
    /// <summary>Opens the scope each startup run resolves its services in.</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="BillingScheduledJobsStartup"/> class.</summary>
    /// <param name="scopeFactory">Opens the scope each startup run resolves its services in.</param>
    public BillingScheduledJobsStartup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Starts every billing job.</summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A <see cref="Task"/> that completes once the first run of every job is scheduled.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<RecurringJobs>();
        var schedules = jobs.Schedules;

        await jobs.StartAsync<ProcessBillableItemsCronCommand>(schedules.ProcessBillableItems, cancellationToken);
        await jobs.StartAsync<CheckOverdueInvoicesCronCommand>(schedules.CheckOverdueInvoices, cancellationToken);
        await jobs.StartAsync<ProcessRenewalsCronCommand>(schedules.ProcessRenewals, cancellationToken);
        await jobs.StartAsync<SendRenewalRemindersCronCommand>(schedules.SendRenewalReminders, cancellationToken);
        await jobs.StartAsync<SuspendOverdueServicesCronCommand>(schedules.SuspendOverdueServices, cancellationToken);
        await jobs.StartAsync<SyncServerAccountCountsCronCommand>(schedules.SyncServerAccountCounts, cancellationToken);
        await jobs.StartAsync<AutoTerminateSuspendedCronCommand>(schedules.AutoTerminateSuspended, cancellationToken);
        await jobs.StartAsync<ReconcileGatewayPaymentsCronCommand>(schedules.ReconcileGatewayPayments, cancellationToken);
        await jobs.StartAsync<DispatchFulfilmentCronCommand>(schedules.DispatchFulfilment, cancellationToken);
    }

    /// <summary>
    /// No-op: nothing needs cleanup on shutdown. A run that was scheduled in memory is lost with the
    /// process, and the next start catches the job up from its last recorded success.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
