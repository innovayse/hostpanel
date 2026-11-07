using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Marks the public plugin callback door as measured against the callback budget of the caller's address. The work is
/// <see cref="PluginCallbackRateLimitFilter"/>, built per request; this attribute is only the mark a controller carries.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginCallbackRateLimitAttribute : TypeFilterAttribute
{
    /// <summary>Marks a controller as the public callback door.</summary>
    public PluginCallbackRateLimitAttribute()
        : base(typeof(PluginCallbackRateLimitFilter))
    {
    }
}
