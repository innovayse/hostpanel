namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultStoredMethod;

/// <summary>Makes one of the authenticated client's stored methods the default at its payment plugin.</summary>
/// <remarks>Carries no client id: the consent is found for the caller's own account only.</remarks>
/// <param name="ConsentId">The active consent that covers the method; the controller overlays it from the route.</param>
public sealed record SetDefaultStoredMethodCommand(int ConsentId);
