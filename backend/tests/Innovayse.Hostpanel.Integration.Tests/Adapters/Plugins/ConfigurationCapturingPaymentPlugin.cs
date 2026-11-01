using Innovayse.SDK.Plugins.Payment;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Minimal payment plugin used as the registered implementation type; keeps the configuration the resolver composed.</summary>
internal sealed class ConfigurationCapturingPaymentPlugin : IPaymentPlugin
{
    /// <summary>Initializes the fake with the composed configuration for assertions.</summary>
    /// <param name="configuration">The configuration the resolver composed.</param>
    public ConfigurationCapturingPaymentPlugin(IConfiguration configuration) => Configuration = configuration;

    /// <summary>Gets the configuration the resolver passed in.</summary>
    public IConfiguration Configuration { get; }

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
