using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Matches a request against the routes a manifest declares. The manifest reader has already refused a route that
/// is not lowercase literal segments and <c>{parameter}</c> segments, and two templates that differ only in a
/// parameter name, so the only ambiguity left is a literal next to a parameter, settled in favour of the literal.
/// </summary>
public static class PluginRouteMatcher
{
    /// <summary>Finds the declared route a request goes to.</summary>
    /// <param name="routes">The routes of one area of the manifest.</param>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="path">The path below the plugin's prefix, without a leading slash.</param>
    /// <returns>
    /// The route with the most literal segments among those of the same method and segment count whose literals
    /// equal the request's, ignoring case, or <see langword="null"/> when none matches. An empty segment, such as a
    /// trailing slash, matches no parameter and so no route.
    /// </returns>
    public static PluginRouteMatch? Match(IReadOnlyList<PluginRoute> routes, PluginRouteMethod method, string? path)
    {
        ArgumentNullException.ThrowIfNull(routes);

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var segments = path.Split('/');
        PluginRouteMatch? best = null;
        var bestLiterals = -1;

        foreach (var route in routes)
        {
            if (route.Method != method)
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!TryBind(route.Path, segments, values, out var literals) || literals <= bestLiterals)
            {
                continue;
            }

            best = new PluginRouteMatch(route, values);
            bestLiterals = literals;
        }

        return best;
    }

    /// <summary>Binds the segments of a request to one template.</summary>
    /// <param name="template">The declared path, such as <c>/invoices/{id}</c>.</param>
    /// <param name="segments">The request's path segments.</param>
    /// <param name="values">Receives the parameter values.</param>
    /// <param name="literals">The number of literal segments that matched.</param>
    /// <returns><see langword="true"/> when every segment fits the template.</returns>
    private static bool TryBind(string template, string[] segments, Dictionary<string, string> values, out int literals)
    {
        literals = 0;
        var declared = template.TrimStart('/').Split('/');
        if (declared.Length != segments.Length)
        {
            return false;
        }

        for (var i = 0; i < declared.Length; i++)
        {
            if (declared[i].StartsWith('{'))
            {
                if (segments[i].Length == 0)
                {
                    return false;
                }

                values[declared[i][1..^1]] = segments[i];
            }
            else if (string.Equals(declared[i], segments[i], StringComparison.OrdinalIgnoreCase))
            {
                literals++;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
