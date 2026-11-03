using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Sets the web server's body limit for one plugin request to the limit derived from the configured cap (<see cref="PluginApiOptions.ServerBodyLimit"/>), before anything
/// reads the body. See <see cref="PluginBodyLimitAttribute"/> for why the limit is neither the server's default nor none.
/// </summary>
public sealed class PluginBodyLimitFilter : IResourceFilter
{
    /// <summary>The configured body cap.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="PluginBodyLimitFilter"/> class.</summary>
    /// <param name="options">The configured body cap.</param>
    public PluginBodyLimitFilter(IOptions<PluginApiOptions> options)
    {
        _options = options;
    }

    /// <summary>Sets the server's limit for this request, before anything reads the body.</summary>
    /// <param name="context">The request being routed to the action.</param>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        // A server that does not expose the feature (an in-memory test server) has no limit to set; one that has it
        // read-only has already started reading the body, and its limit then stands.
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = _options.Value.ServerBodyLimit;
        }
    }

    /// <summary>Does nothing after the action.</summary>
    /// <param name="context">The finished request.</param>
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
