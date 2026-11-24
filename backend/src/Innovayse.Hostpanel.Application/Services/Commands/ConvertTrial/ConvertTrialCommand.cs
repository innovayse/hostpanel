namespace Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;

/// <summary>
/// Converts a running free trial into a paid service: the service awaits the payment of its first paid period,
/// and the invoice for that period is raised. Repeating it on a trial already awaiting payment only raises a
/// missing invoice.
/// </summary>
/// <param name="ServiceId">The trial service; for a route, set from the route id.</param>
public sealed record ConvertTrialCommand(int ServiceId);
