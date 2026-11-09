using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Security.Extensions;

/// <summary>
/// Puts an HTTP client that dials admin-entered hosts behind <see cref="OutboundConnector"/>.
/// </summary>
public static class OutboundHttpClientBuilderExtensions
{
    /// <summary>
    /// Replaces the client's primary handler with one whose every connection goes through
    /// <see cref="OutboundConnector.ConnectAsync"/>, with the connector's connect timeout. A redirect
    /// that is followed is dialled through the same callback, so following one cannot reach a refused
    /// address; a client that sends secrets in the request should not follow one at all.
    /// </summary>
    /// <param name="builder">The client registration.</param>
    /// <param name="followRedirects">
    /// <see langword="false"/> to stop the handler following any redirect, so a 3xx reply reaches the
    /// caller as it is and the request, with its body and headers, is never resent to another location.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHttpClientBuilder AddOutboundAddressGuard(this IHttpClientBuilder builder, bool followRedirects = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var connector = sp.GetRequiredService<OutboundConnector>();
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = followRedirects,
                ConnectTimeout = connector.ConnectTimeout,
                ConnectCallback = connector.ConnectAsync,
            };
        });
    }
}
