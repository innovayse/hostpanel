using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>
/// Refuses startup when plain http preview origins are configured outside Development or are not loopback origins. This
/// is the one thing keeping them off a production host, so it refuses rather than ignores: a setting that is silently
/// dropped would hide the fact that someone tried.
/// </summary>
public sealed class PluginPreviewOptionsValidator : IValidateOptions<PluginPreviewOptions>
{
    /// <summary>The environment the host runs in.</summary>
    private readonly IHostEnvironment _environment;

    /// <summary>Initializes the validator.</summary>
    /// <param name="environment">The environment the host runs in.</param>
    public PluginPreviewOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming the setting.</returns>
    public ValidateOptionsResult Validate(string? name, PluginPreviewOptions options)
    {
        var setting = $"{PluginPreviewOptions.SectionName}:{nameof(PluginPreviewOptions.DevelopmentHttpOrigins)}";

        // Blank is how the dev compose file says "none", so it counts as unset in every environment.
        var origins = options.DevelopmentHttpOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToList();
        if (origins.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        if (!_environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail($"{setting} may be set only in the Development environment.");
        }

        return origins.All(origin => PluginFrameOrigin.FromLoopbackHttp(origin) is not null)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{setting} must hold exact http origins on a loopback host.");
    }
}
