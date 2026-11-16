using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Clients.Commands.RecheckVatNumbersCron;

/// <summary>
/// Scheduled command that checks clients' VAT numbers again. Dispatched daily by <c>BillingScheduledJobsStartup</c> at the
/// hour set in <c>ScheduledJobsOptions</c>.
/// </summary>
/// <remarks>
/// A client is picked when its number was never judged or its last check is older than <c>VatRecheckAfterDays</c>, so a
/// number that stopped being valid stops the reverse charge for new invoices within that many days.
/// </remarks>
public sealed record RecheckVatNumbersCronCommand : IRecurringJob;
