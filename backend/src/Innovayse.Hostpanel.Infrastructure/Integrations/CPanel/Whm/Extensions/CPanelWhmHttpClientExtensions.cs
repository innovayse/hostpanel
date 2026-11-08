using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Extensions;

/// <summary>
/// The one registration of the named WHM HTTP client, used by the application and by the tests
/// that prove what it does, so a test can never describe a chain the application does not build.
/// </summary>
public static class CPanelWhmHttpClientExtensions
{
    /// <summary>
    /// Registers the <see cref="HttpClientNames.CPanelWhm"/> client: no request logging, the
    /// outbound address guard with redirects off, and the WHM resilience profile with no retry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Request logging is removed because <c>createacct</c> and <c>passwd</c> carry a password,
    /// which travels in the POST body and never the URL, and no request detail should reach a log
    /// line. The request also holds the token and the password, so a redirect is never followed: a
    /// 307 or 308 would resend both to another host. A redirect reads as an unreadable reply.
    /// </remarks>
    public static IServiceCollection AddCPanelWhmHttpClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(HttpClientNames.CPanelWhm, client =>
        {
            client.Timeout = HttpBackstopTimeouts.CPanelWhm;
        })
            .RemoveAllLoggers()
            .AddOutboundAddressGuard(followRedirects: false)
            .AddNoRetryResilience(o => o.CPanelWhm);

        return services;
    }
}
