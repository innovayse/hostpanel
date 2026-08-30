namespace Innovayse.Application.Services.Queries.GetMyServiceCancellationStatus;

using Innovayse.Application.Services.Common;

/// <summary>
/// Query for a signed-in client to read the cancellation status of one of their own services.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. <c>GetCancellationStatusQuery</c>, which this delegates to,
/// names no caller and is what any future staff-side route would dispatch.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
public sealed record GetMyServiceCancellationStatusQuery(int ServiceId) : ICallerScopedServiceMessage;
