namespace Innovayse.Application.Services.Commands.CancelMyService;

using Innovayse.Application.Services.Common;

/// <summary>
/// Command for a signed-in client to request cancellation of one of their own services.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. <c>CancelServiceCommand</c>, which this delegates to, names no
/// caller and is what any future staff-side route would dispatch.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
/// <param name="Type">Cancellation type: <c>Immediate</c> or <c>EndOfBillingPeriod</c>.</param>
/// <param name="Reason">Optional client-supplied reason.</param>
public sealed record CancelMyServiceCommand(int ServiceId, string Type, string? Reason)
    : ICallerScopedServiceMessage;
