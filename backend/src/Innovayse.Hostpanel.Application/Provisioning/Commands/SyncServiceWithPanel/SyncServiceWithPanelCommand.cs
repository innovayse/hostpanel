namespace Innovayse.Hostpanel.Application.Provisioning.Commands.SyncServiceWithPanel;

/// <summary>
/// Reconciles a provisioned service's hosting account with its panel: the platform is the source of truth, so the status and the
/// package the platform holds are applied to the panel, and the panel's IP address is recorded on the service.
/// </summary>
/// <param name="ServiceId">The client service identifier to reconcile. Overlaid from the route.</param>
/// <param name="PushPassword">Whether the password stored on the service is also set on the panel. A panel never reports its passwords, so this is the staff's explicit choice.</param>
public sealed record SyncServiceWithPanelCommand(int ServiceId, bool PushPassword);
