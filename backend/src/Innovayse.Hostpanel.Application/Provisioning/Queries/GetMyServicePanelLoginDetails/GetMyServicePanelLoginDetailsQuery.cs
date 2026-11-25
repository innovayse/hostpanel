using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServicePanelLoginDetails;

/// <summary>
/// Query for a signed-in client to read how they sign in by hand to the panel of one of their own services, when the panel cannot
/// issue a single sign on link. It carries a service id and no client id: which account the service must belong to is resolved in the
/// handler from the credential. The admin route dispatches <c>GetPanelLoginDetailsQuery</c> directly.
/// </summary>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
public sealed record GetMyServicePanelLoginDetailsQuery(int ServiceId) : ICallerScopedServiceMessage;
