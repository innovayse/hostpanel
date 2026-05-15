namespace Innovayse.Application.Notifications.Services;

using Fluid;

/// <summary>Renders Liquid templates with provided model data.</summary>
public sealed class TemplateRenderer
{
    /// <summary>The Fluid parser instance.</summary>
    private readonly FluidParser _parser = new();

    /// <summary>
    /// Renders a Liquid template string with the given model.
    /// </summary>
    /// <param name="template">The Liquid template string to render.</param>
    /// <param name="model">The data model to expose to the template.</param>
    /// <returns>The rendered output string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the template contains a parse error.</exception>
    public async Task<string> RenderAsync(string template, object model)
    {
        if (_parser.TryParse(template, out var fluidTemplate, out var error))
        {
            var context = new TemplateContext(model);
            return await fluidTemplate.RenderAsync(context);
        }

        throw new InvalidOperationException($"Template parse error: {error}");
    }
}
