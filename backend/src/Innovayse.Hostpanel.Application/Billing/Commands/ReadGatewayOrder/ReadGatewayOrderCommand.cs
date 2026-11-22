namespace Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;

/// <summary>
/// Reads one gateway order of a payment plugin now, because the plugin's callback said it probably changed. A hint and never
/// proof: the order is found by the plugin's own id, and whatever it turns out to be comes from the plugin's status read
/// (<c>IPaymentPlugin.GetStatusAsync</c>), never from the callback. Dispatched in the process by
/// <c>PluginOrderReadService</c>, with the plugin id taken from the call's binding.
/// </summary>
/// <param name="PluginId">The payment plugin whose callback asked, from the call's binding and not from anything the plugin sent.</param>
/// <param name="GatewayOrderId">The gateway-side order id the plugin named.</param>
public sealed record ReadGatewayOrderCommand(string PluginId, string GatewayOrderId);
