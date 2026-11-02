using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Marks a plugin door as measured against the rate limit tier of the route it reaches. The work is
/// <see cref="PluginRateLimitFilter"/>, built per request; this attribute is only the mark a controller carries, with the
/// panel it serves.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginRateLimitAttribute : TypeFilterAttribute
{
    /// <summary>Marks a controller as the door to one panel's plugin routes.</summary>
    /// <param name="area">The panel the controller serves.</param>
    public PluginRateLimitAttribute(PluginArea area)
        : base(typeof(PluginRateLimitFilter))
    {
        Arguments = [area];
    }
}
