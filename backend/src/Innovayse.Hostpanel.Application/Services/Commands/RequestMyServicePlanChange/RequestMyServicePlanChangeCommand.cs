using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Services.Commands.RequestMyServicePlanChange;

/// <summary>
/// Command for a signed-in client to move one of their own services to another plan of its product group.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved inside the handler
/// from the credential, so the scoping cannot be separated from the message the way an id in the body can.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
/// <param name="ToProductId">The plan to move to, one of the options the client was shown.</param>
public sealed record RequestMyServicePlanChangeCommand(int ServiceId, int ToProductId) : ICallerScopedServiceMessage;
