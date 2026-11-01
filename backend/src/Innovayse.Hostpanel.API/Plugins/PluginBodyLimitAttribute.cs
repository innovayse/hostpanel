using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Hands the size of a plugin route's request body to the plugin call alone: it lifts the web server's own per request
/// body limit for these routes, because <c>PluginApi:MaxBodyBytes</c> (up to 64 MiB) is the limit and the server's default
/// (about 28.6 MiB) is lower than the ceiling a deployment may configure.
/// </summary>
/// <remarks>
/// Without it a chunked body that passes the server's limit but not the configured cap would be cut off by the server
/// with its own bare 413, and a body between the two would answer differently from one above both. With it, the one
/// reader of the body, <c>PluginCallRunner</c>, is the only limit: it refuses a declared length over the cap without
/// reading and stops reading the moment a body passes it, so every oversize request answers 413
/// <c>PLUGIN_REQUEST_TOO_LARGE</c>. The body is read only after authorisation, and never past the cap plus one chunk.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginBodyLimitAttribute : Attribute, IResourceFilter
{
    /// <summary>Lifts the server's body limit for this request, before anything reads the body.</summary>
    /// <param name="context">The request being routed to the action.</param>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        // A server that does not expose the feature (an in-memory test server) has no limit to lift; one that has it
        // read-only has already started reading the body, and its limit then stands.
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = null;
        }
    }

    /// <summary>Does nothing after the action.</summary>
    /// <param name="context">The finished request.</param>
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
