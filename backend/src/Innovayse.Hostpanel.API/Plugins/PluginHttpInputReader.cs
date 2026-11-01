using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Reads the HTTP request of a plugin route into the framework-free <see cref="PluginHttpInput"/> the use case takes.
/// The one place the two plugin controllers bind the request, so they bind it the same way.
/// </summary>
public static class PluginHttpInputReader
{
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
}
