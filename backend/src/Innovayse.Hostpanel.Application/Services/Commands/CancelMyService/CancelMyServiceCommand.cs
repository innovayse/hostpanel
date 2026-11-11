using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Commands.CancelMyService;

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
/// <param name="Type">When the cancellation takes effect.</param>
/// <param name="Reason">Optional client-supplied reason.</param>
public sealed record CancelMyServiceCommand(int ServiceId, CancellationType Type, string? Reason)
    : ICallerScopedServiceMessage;
