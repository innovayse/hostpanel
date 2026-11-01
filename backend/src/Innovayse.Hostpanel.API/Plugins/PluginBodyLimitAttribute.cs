using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Hands the size of a plugin route's request body to the plugin call: it sets the web server's own per request body
/// limit for these routes to <c>PluginApi:MaxBodyBytes</c> plus a small allowance, because the cap (up to 64 MiB) may be
/// above the server's default (about 28.6 MiB) and the server's limit must never be the one that answers first.
/// </summary>
/// <remarks>
/// The work is <see cref="PluginBodyLimitFilter"/>, built per request with the configured cap injected; this attribute is
/// only the mark a controller carries. The limit is the cap plus one read chunk and the chunk framing (see
/// <see cref="Innovayse.Hostpanel.Application.Common.Settings.PluginApiOptions.ServerBodyLimit"/>), not none:
/// <c>PluginBodyReader</c>, the one reader of the body, stops at the first read that passes the cap, which is never more
/// than one chunk beyond it, so the runner answers its own 413 <c>PLUGIN_REQUEST_TOO_LARGE</c> before the server's limit
/// is reached. A request the runner never reads (an unknown plugin, an undeclared route, a refused caller) is bounded by
/// the same limit, so an unread body of any size cannot make the server drain gigabytes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginBodyLimitAttribute : TypeFilterAttribute
{
    /// <summary>Marks a controller; the filter that does the work is <see cref="PluginBodyLimitFilter"/>.</summary>
    public PluginBodyLimitAttribute()
        : base(typeof(PluginBodyLimitFilter))
    {
    }
}
