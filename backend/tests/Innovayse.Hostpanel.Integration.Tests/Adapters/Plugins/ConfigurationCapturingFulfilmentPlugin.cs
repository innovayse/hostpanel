using Innovayse.SDK.Plugins.Fulfilment;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Minimal fulfilment plugin used as a registered implementation type; keeps the configuration it was activated with.</summary>
internal sealed class ConfigurationCapturingFulfilmentPlugin : IFulfilmentPlugin
{
    /// <summary>Initializes the fake with the composed configuration for assertions.</summary>
    /// <param name="configuration">The configuration the activator composed.</param>
    public ConfigurationCapturingFulfilmentPlugin(IConfiguration configuration) => Configuration = configuration;

    /// <summary>Gets the configuration the activator passed in.</summary>
    public IConfiguration Configuration { get; }

    /// <inheritdoc/>
    public Task OnInvoicePaidAsync(InvoicePaidContext context, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<RefundAdvice> AdviseRefundAsync(RefundAdviceContext context, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task OnInvoiceRefundedAsync(InvoiceRefundedContext context, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task OnSubscriptionLapsedAsync(SubscriptionLapsedContext context, CancellationToken ct)
        => throw new NotSupportedException();
}
