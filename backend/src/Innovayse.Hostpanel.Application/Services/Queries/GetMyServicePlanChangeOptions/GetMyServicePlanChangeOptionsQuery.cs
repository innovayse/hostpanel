using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServicePlanChangeOptions;

/// <summary>
/// Query for a signed-in client to list the plans one of their own services can move to, each with the prorated
/// amount shown before the client confirms.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved inside the handler
/// from the credential, so the scoping cannot be separated from the message the way an id in the body can.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
public sealed record GetMyServicePlanChangeOptionsQuery(int ServiceId) : ICallerScopedServiceMessage;
