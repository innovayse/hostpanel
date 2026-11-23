using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>Query filters shared by the Services module's billing sweeps.</summary>
public static class ClientServiceQueryExtensions
{
    /// <summary>
    /// Leaves out the services whose free trial is still running (<see cref="TrialStatus.Active"/> or
    /// <see cref="TrialStatus.AwaitingPayment"/>), so renewal, reminder, suspension and termination sweeps never
    /// touch them; a converted or terminated trial is an ordinary service again and stays in.
    /// </summary>
    /// <param name="services">The services to filter.</param>
    /// <returns>The services that are not in a running trial.</returns>
    public static IQueryable<ClientService> NotInTrial(this IQueryable<ClientService> services) =>
        services.Where(s => s.TrialStatus == null
            || (s.TrialStatus != TrialStatus.Active && s.TrialStatus != TrialStatus.AwaitingPayment));
}
