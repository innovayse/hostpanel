namespace Innovayse.Application.Notifications.Services;

using System.Collections;
using System.Reflection;
using Fluid;
using Innovayse.Application.Notifications.Interfaces;

/// <summary>
/// Renders Liquid templates with the caller's model plus the storefront's brand.
/// </summary>
/// <remarks>
/// Every render sees <c>brand</c> (<c>primary</c>, <c>primary_dark</c>, <c>on_primary</c>,
/// <c>accent</c>, <c>logo_url</c>) and <c>site</c> (<c>name</c>) beside whatever the caller
/// passed, so a stored template can follow the operator's colours without any of the three
/// callers changing. A caller's own <c>brand</c> or <c>site</c> member wins over the injected
/// one — the model is the caller's contract, and this class must not silently shadow it.
/// </remarks>
/// <param name="branding">Where the brand comes from.</param>
public sealed class TemplateRenderer(IEmailBrandingProvider branding)
{
    /// <summary>The Fluid parser instance.</summary>
    private readonly FluidParser _parser = new();

    /// <summary>
    /// Renders a Liquid template string with the given model and the current brand.
    /// </summary>
    /// <param name="template">The Liquid template string to render.</param>
    /// <param name="model">The data model to expose to the template.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered output string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the template contains a parse error.</exception>
    public async Task<string> RenderAsync(string template, object model, CancellationToken ct = default)
    {
        if (!_parser.TryParse(template, out var fluidTemplate, out var error))
        {
            throw new InvalidOperationException($"Template parse error: {error}");
        }

        var context = new TemplateContext(model);

        var brand = await branding.GetAsync(ct);
        if (!HasMember(model, "brand"))
        {
            context.SetValue("brand", brand.ToBrandModel());
        }

        if (!HasMember(model, "site"))
        {
            context.SetValue("site", brand.ToSiteModel());
        }

        return await fluidTemplate.RenderAsync(context);
    }

    /// <summary>
    /// Whether the caller's model already exposes a name, so the injected object stays out
    /// of its way. Looks at dictionary keys and public properties by exact name, which is
    /// how Fluid resolves them — a model with a <c>Site</c> key does not answer
    /// <c>{{ site }}</c>, so it must not suppress the injected one either.
    /// </summary>
    /// <param name="model">The caller's model.</param>
    /// <param name="name">The Liquid name.</param>
    /// <returns>True when the model would answer <c>{{ name }}</c> itself.</returns>
    private static bool HasMember(object model, string name)
    {
        if (model is IDictionary dictionary)
        {
            return dictionary.Keys.Cast<object>().Any(k => k is string s && s == name);
        }

        return model.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is not null;
    }
}
