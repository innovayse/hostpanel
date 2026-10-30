namespace Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;

/// <summary>
/// Activates a pending service of a digital product without provisioning it: a digital product has no hosting
/// account, so no server is chosen and no panel is called. The caller decides the product is digital.
/// </summary>
/// <param name="ServiceId">The pending service to activate.</param>
public sealed record ActivateDigitalServiceCommand(int ServiceId);
