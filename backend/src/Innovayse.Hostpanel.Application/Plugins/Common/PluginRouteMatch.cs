using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>A declared route that matched a request path, with the values its parameters took.</summary>
/// <param name="Route">The declared route that matched.</param>
/// <param name="Values">The route parameters by name, as the request wrote them.</param>
public sealed record PluginRouteMatch(PluginRoute Route, IReadOnlyDictionary<string, string> Values);
