namespace Innovayse.Application.Provisioning.Queries.GetMyServiceCPanelSsoUrl;

using Innovayse.Application.Services.Common;

/// <summary>
/// Query for a signed-in client to obtain a control-panel single-sign-on URL for one of their own
/// services.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin route, which may sign into any client's account,
/// dispatches <c>GetCPanelSsoUrlQuery</c> directly.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
public sealed record GetMyServiceCPanelSsoUrlQuery(int ServiceId) : ICallerScopedServiceMessage;
