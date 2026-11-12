using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Payment plugin that also opts in to <see cref="IPaymentReturnPlugin"/>. The activation tests only need the capability
/// to be present, so no operation does anything.
/// </summary>
internal sealed class ReturnCapablePaymentPlugin : IPaymentReturnPlugin
{
    /// <summary>Initializes the fake; the composed configuration is accepted because the activator always passes it.</summary>
    /// <param name="configuration">The configuration the activator composed.</param>
    public ReturnCapablePaymentPlugin(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>Gets the configuration the activator passed in.</summary>
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
    public Task<GatewayPaymentStatus> CompleteAsync(
        string gatewayOrderId, IReadOnlyDictionary<string, string> returnParameters, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
        => throw new NotSupportedException();
}
