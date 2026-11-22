using System.Globalization;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Whose budget an access is counted against: one plugin, one subject (a client account or a gateway order) and one
/// named budget. The subject carries its own kind, so a client and an order of the same text can never share a partition.
/// </summary>
/// <param name="PluginId">The plugin asking.</param>
/// <param name="Subject">What the access is about, written by <see cref="ForClient"/> or <see cref="ForOrder"/>.</param>
/// <param name="Budget">The rate limit tier the access is counted in, for example the portal's <c>ssh-keys</c>.</param>
public sealed record PluginHostAccessKey(string PluginId, string Subject, string Budget)
{
    /// <summary>The prefix of a client subject, as the plugin call filter writes it.</summary>
    private const string ClientPrefix = "client:";

    /// <summary>The prefix of a gateway order subject.</summary>
    private const string OrderPrefix = "order:";

    /// <summary>Builds the key of an access a plugin makes for one client account.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="budget">The rate limit tier the access is counted in.</param>
    /// <returns>The key, partitioned per plugin and client account.</returns>
    public static PluginHostAccessKey ForClient(string pluginId, int clientId, string budget)
    {
        return new PluginHostAccessKey(pluginId, ClientPrefix + clientId.ToString(CultureInfo.InvariantCulture), budget);
    }

    /// <summary>Builds the key of an access a plugin makes for one gateway order.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="gatewayOrderId">The gateway-side order id, as the plugin sent it.</param>
    /// <param name="budget">The rate limit tier the access is counted in.</param>
    /// <returns>The key, partitioned per plugin and gateway order.</returns>
    public static PluginHostAccessKey ForOrder(string pluginId, string gatewayOrderId, string budget)
    {
        return new PluginHostAccessKey(pluginId, OrderPrefix + gatewayOrderId, budget);
    }
}
