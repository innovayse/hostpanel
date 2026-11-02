namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The two facts about a service a plugin slot needs: whose it is and which product it orders.</summary>
/// <param name="ClientId">The client account the service belongs to.</param>
/// <param name="ProductId">The product the service orders.</param>
public sealed record PluginSlotServiceDto(int ClientId, int ProductId);
