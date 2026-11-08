namespace Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;

/// <summary>
/// Activates a pending service of a digital product without provisioning it: a digital product has no hosting
/// account, so no server is chosen and no panel is called. The caller decides the product is digital.
/// </summary>
/// <param name="ServiceId">The pending service to activate.</param>
/// <param name="OrderItemId">
/// The order line the activation is for, or <see langword="null"/> when it is not for a line. A service of that same line
/// that is already Active was activated by an earlier attempt of the line that died before recording it, so activating it
/// again counts as done instead of being refused.
/// </param>
public sealed record ActivateDigitalServiceCommand(int ServiceId, int? OrderItemId = null);
