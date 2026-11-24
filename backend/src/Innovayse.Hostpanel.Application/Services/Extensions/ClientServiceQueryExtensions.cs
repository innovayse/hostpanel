using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>Query filters shared by the Services module's billing sweeps.</summary>
public static class ClientServiceQueryExtensions
{
    /// <summary>
    /// Leaves out the services whose free trial is still running (<see cref="TrialStatus.Active"/>), so renewal,
    /// reminder, suspension and termination sweeps never touch them.
    /// </summary>
    /// <param name="services">The services to filter.</param>
    /// <returns>The services that are not in a running trial.</returns>
    public static IQueryable<ClientService> NotInTrial(this IQueryable<ClientService> services) =>
        services.Where(s => s.TrialStatus == null || s.TrialStatus != TrialStatus.Active);
}
