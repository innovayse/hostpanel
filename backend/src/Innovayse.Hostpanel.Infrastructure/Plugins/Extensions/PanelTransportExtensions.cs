using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// The one registration of <see cref="IPanelTransport"/> and of the named client behind it, used by the application and by
/// the tests that prove what it does, so a test can never describe a chain the application does not build.
/// </summary>
public static class PanelTransportExtensions
{
    /// <summary>
    /// Registers the <see cref="HttpClientNames.PanelTransport"/> client (no request logging, the outbound address guard with
    /// redirects off, a backstop timeout and no retry stage) and <see cref="PanelTransport"/> as the scoped
    /// <see cref="IPanelTransport"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Request logging is removed because a plugin's request addresses may carry arguments in the query string. There is no
    /// resilience pipeline, which is the no retry rule: a panel call can carry a password or change an account, so a request is
    /// never repeated by the host and the plugin owns every retry through the idempotency its operations promise.
    /// </remarks>
    public static IServiceCollection AddPanelTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(HttpClientNames.PanelTransport, client =>
        {
            client.Timeout = HttpBackstopTimeouts.PanelTransport;
        })
            .RemoveAllLoggers()
            .AddOutboundAddressGuard(followRedirects: false);
        services.AddScoped<IPanelTransport, PanelTransport>();

        return services;
    }
}
