using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Fulfilment;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A fulfilment plugin that also implements the endpoints contract, the shape the <c>endpoints</c> capability promises (#818).</summary>
internal sealed class FulfilmentEndpointsPlugin : IFulfilmentPlugin, IPluginEndpoints
{
    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    public FulfilmentEndpointsPlugin(IConfiguration configuration)
    {
    }

    /// <inheritdoc/>
    public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
    {
        return Task.FromResult(PluginReply.NoContent());
    }

    /// <inheritdoc/>
    public Task OnInvoicePaidAsync(InvoicePaidContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<RefundAdvice> AdviseRefundAsync(RefundAdviceContext context, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task OnInvoiceRefundedAsync(InvoiceRefundedContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnSubscriptionLapsedAsync(SubscriptionLapsedContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
