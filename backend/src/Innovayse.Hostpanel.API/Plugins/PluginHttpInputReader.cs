using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Reads the HTTP request of a plugin route into the framework-free <see cref="PluginHttpInput"/> the use case takes.
/// The one place the two plugin controllers bind the request, so they bind it the same way.
/// </summary>
public static class PluginHttpInputReader
{
    /// <summary>The headers, besides <c>X-*</c> ones, a callback sender may need: the media type, the size, the sender and the usual signature and replay headers.</summary>
    private static readonly HashSet<string> CallbackHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type",
        "Content-Length",
        "User-Agent",
        "Signature",
        "Digest",
        "Content-Digest",
        "Date",
        "Idempotency-Key",
        "Webhook-Id",
        "Webhook-Timestamp",
        "Webhook-Signature",
    };

    /// <summary>
    /// The <c>X-*</c> headers that are never passed on: the proxy's view of the connection (a plugin must not trust a
    /// forwarded address a caller can set) and the headers the host's own sessions and services use.
    /// </summary>
    private static readonly HashSet<string> RefusedExtensionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-Real-IP",
        "X-Language",
        "X-Requested-With",
        "X-Logout-Key",
        "X-Service-Key",
        "X-Renewed-Access-Token",
    };

    /// <summary>Reads the method, the path, the query and the body stream of a request.</summary>
    /// <param name="request">The incoming request. Its body is passed on unread.</param>
    /// <param name="path">The catch-all route value: the path below the plugin's own prefix, without a leading slash.</param>
    /// <returns>The input.</returns>
    /// <exception cref="InvalidOperationException">The method is not one a plugin route declares; the controllers map only those.</exception>
    public static PluginHttpInput Read(HttpRequest request, string? path)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The enum names are the method names, so the parse needs no table; a numeric text is not a method.
        if (!Enum.TryParse<PluginRouteMethod>(request.Method, ignoreCase: true, out var method)
            || !Enum.IsDefined(method)
            || char.IsDigit(request.Method[0]))
        {
            throw new InvalidOperationException($"{request.Method} is not a plugin route method.");
        }

        var query = request.Query.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OfType<string>().ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new PluginHttpInput(method, path ?? string.Empty, query, request.Body, request.ContentType, request.ContentLength);
    }

    /// <summary>
    /// Reads a request to the public callback door: everything <see cref="Read"/> reads, plus the headers a sender may sign or
    /// need, exactly as received. Only the callback door passes headers on, and only an allow-list of them (see
    /// <see cref="IsPassedOn"/>): never <c>Cookie</c>, <c>Authorization</c>, <c>Proxy-Authorization</c>, <c>X-Forwarded-*</c>
    /// or a header of the host's own sessions, so a plugin cannot receive a credential that reached the door by accident.
    /// The other doors' headers carry the caller's own credentials and are never handed over.
    /// </summary>
    /// <param name="request">The incoming request. Its body is passed on unread and unchanged.</param>
    /// <param name="path">The catch-all route value: the path below the plugin's callback prefix.</param>
    /// <returns>The input, with the headers.</returns>
    /// <exception cref="InvalidOperationException">The method is not one a plugin route declares; the controller maps only those.</exception>
    public static PluginHttpInput ReadCallback(HttpRequest request, string? path)
    {
        var headers = request.Headers.Where(pair => IsPassedOn(pair.Key)).ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OfType<string>().ToList(),
            StringComparer.OrdinalIgnoreCase);

        return Read(request, path) with { Headers = headers };
    }

    /// <summary>Decides whether a request header is handed to a plugin on the callback door.</summary>
    /// <param name="name">The header name, in any case.</param>
    /// <returns>
    /// <see langword="true"/> for a name on the explicit list, or an <c>X-*</c> name that is not <c>X-Forwarded-*</c> and not one
    /// of the host's own; <see langword="false"/> for everything else, including <c>Cookie</c>, <c>Authorization</c>,
    /// <c>Proxy-Authorization</c> and <c>Host</c>.
    /// </returns>
    public static bool IsPassedOn(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (CallbackHeaders.Contains(name))
        {
            return true;
        }

        return name.StartsWith("X-", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
            && !RefusedExtensionHeaders.Contains(name);
    }
}
