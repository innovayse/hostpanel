using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.Store;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Minimal payment plugin that takes the plugin-wide <see cref="IPluginStore"/> in its constructor, like a gateway keeping order data between calls.</summary>
internal sealed class StoreTakingPaymentPlugin : IPaymentPlugin
{
    /// <summary>Initializes the fake with the configuration and the store the host provided.</summary>
    /// <param name="configuration">The configuration the activator composed.</param>
    /// <param name="store">The plugin-wide store the host provided.</param>
    public StoreTakingPaymentPlugin(IConfiguration configuration, IPluginStore store)
    {
        Configuration = configuration;
        Store = store;
    }

    /// <summary>Gets the configuration the activator passed in.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>Gets the store the host provided.</summary>
    public IPluginStore Store { get; }

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
        => throw new NotSupportedException();
}
