using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Security.Extensions;

/// <summary>
/// Stops an HTTP client the host hands to a plugin from following redirects, as the plugin contract requires of a host.
/// </summary>
public static class NoRedirectHttpClientBuilderExtensions
{
    /// <summary>
    /// Makes the client return a 3xx reply as it is. A client that followed one would resend the plugin's authorization
    /// header, and for some handlers its request body, to whatever address the reply names, which may be another host; the
    /// plugin decides for itself whether the new address deserves the credential. Only the redirect rule is changed: the
    /// factory's own handler, with its connection lifetime, stays.
    /// </summary>
    /// <param name="builder">The client registration.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHttpClientBuilder AddNoRedirects(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseSocketsHttpHandler((handler, _) => handler.AllowAutoRedirect = false);
    }
}
