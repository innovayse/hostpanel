namespace Innovayse.Hostpanel.Application.Provisioning.Commands.RefreshServiceFromPanel;

/// <summary>Reads a provisioned service's hosting account from its panel and records what the platform keeps of it, the assigned IP address. Never changes the panel.</summary>
/// <param name="ServiceId">The client service identifier to refresh.</param>
public sealed record RefreshServiceFromPanelCommand(int ServiceId);
