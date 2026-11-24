namespace Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;

/// <summary>
/// Ends a running free trial without converting it: the unpaid invoice of a trial awaiting payment is cancelled,
/// then the service is terminated.
/// </summary>
/// <param name="ServiceId">The trial service; for a route, set from the route id.</param>
/// <param name="Reason">Why the trial ends, passed on to the panel; or <see langword="null"/> for the default reason.</param>
public sealed record TerminateTrialCommand(int ServiceId, string? Reason = null);
