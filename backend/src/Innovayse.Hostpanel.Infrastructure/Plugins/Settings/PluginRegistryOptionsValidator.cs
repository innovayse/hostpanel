using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>Refuses startup when the plugin registry configuration is unsafe or unusable, so a typo cannot silently weaken it.</summary>
public sealed class PluginRegistryOptionsValidator : IValidateOptions<PluginRegistryOptions>
{
    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming the offending setting.</returns>
    public ValidateOptionsResult Validate(string? name, PluginRegistryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Timeout < PluginRegistryOptions.MinTimeout || options.Timeout > PluginRegistryOptions.MaxTimeout)
        {
            return Fail(nameof(PluginRegistryOptions.Timeout), $"must be between {PluginRegistryOptions.MinTimeout} and {PluginRegistryOptions.MaxTimeout}");
        }

        if (options.MaxDownloadBytes < 1 || options.MaxDownloadBytes > PluginRegistryOptions.MaxAllowedDownloadBytes)
        {
            return Fail(nameof(PluginRegistryOptions.MaxDownloadBytes), $"must be between 1 and {PluginRegistryOptions.MaxAllowedDownloadBytes}");
        }

        // An address that is set must be usable even while the registry is off, so switching it on later cannot fail late.
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return options.Enabled
                ? Fail(nameof(PluginRegistryOptions.BaseUrl), "must be set when the registry is enabled")
                : ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return Fail(nameof(PluginRegistryOptions.BaseUrl), "must be an absolute https URL with a host, no user info, no query and no fragment");
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>Builds the failure for one setting.</summary>
    /// <param name="member">The offending setting name.</param>
    /// <param name="problem">What is wrong with it.</param>
    /// <returns>The failure.</returns>
    private static ValidateOptionsResult Fail(string member, string problem)
    {
        return ValidateOptionsResult.Fail(
            $"{PluginRegistryOptions.SectionName}:{member} {problem} (set {PluginRegistryOptions.SectionName}__{member}).");
    }
}
