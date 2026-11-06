namespace Innovayse.Hostpanel.Application.Services.Commands.SetServiceAutoRenew;

/// <summary>Staff switch the automatic billing of one service on or off, leaving its other automation settings as they are.</summary>
/// <param name="ServiceId">The service primary key; the route id wins over any id in the body.</param>
/// <param name="Value">
/// Whether the billing runs look after the service (renewal invoices, overdue suspension, termination of a long
/// suspension); <see langword="false"/> means staff bill it by hand.
/// </param>
public sealed record SetServiceAutoRenewCommand(int ServiceId, bool Value);
