using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Keeps MVC from reading a form body on a plugin route, so <c>PluginCallRunner</c> is the only reader of the request body.
/// </summary>
/// <remarks>
/// MVC builds its value providers before the action runs, and for a urlencoded or multipart request the form providers
/// read and buffer the whole form with no regard for <c>PluginApi:MaxBodyBytes</c>; the stream would then be consumed
/// and the plugin would get an empty body. A plugin route binds nothing but its route values and query, so only the
/// route and query providers are kept and every other provider, the form ones included, is removed.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NoFormBindingAttribute : Attribute, IResourceFilter
{
    /// <summary>Removes every value provider that is not the route's or the query string's, before any is created.</summary>
    /// <param name="context">The request being routed to the action.</param>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        for (var index = context.ValueProviderFactories.Count - 1; index >= 0; index--)
        {
            if (context.ValueProviderFactories[index] is not (RouteValueProviderFactory or QueryStringValueProviderFactory))
            {
                context.ValueProviderFactories.RemoveAt(index);
            }
        }
    }

    /// <summary>Does nothing after the action.</summary>
    /// <param name="context">The finished request.</param>
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
