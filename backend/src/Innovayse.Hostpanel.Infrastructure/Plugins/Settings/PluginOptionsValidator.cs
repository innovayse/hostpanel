using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>Refuses startup when the plugin settings are not usable.</summary>
public sealed class PluginOptionsValidator : IValidateOptions<PluginOptions>
{
    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming the offending key.</returns>
    public ValidateOptionsResult Validate(string? name, PluginOptions options)
    {
        if (options.MaxEntryCount <= 0)
        {
            return Fail(nameof(PluginOptions.MaxEntryCount), "must be greater than zero");
        }

        if (options.MaxUncompressedBytes <= 0)
        {
            return Fail(nameof(PluginOptions.MaxUncompressedBytes), "must be greater than zero");
        }

        if (options.MaxViewBytes < 1 || options.MaxViewBytes > PluginOptions.MaxViewBytesCeiling)
        {
            return Fail(
                nameof(PluginOptions.MaxViewBytes),
                $"must be between 1 and {PluginOptions.MaxViewBytesCeiling} bytes");
        }

        foreach (var digest in options.TrustedPackageSha256)
        {
            if (digest is null || digest.Length != 64 || !digest.All(Uri.IsHexDigit))
            {
                return Fail(nameof(PluginOptions.TrustedPackageSha256), "must hold only 64-character hex SHA-256 digests");
            }
        }

        // Built-in detection is "the folder is under the build output's plugins folder", so the
        // upload folder must be neither that folder, inside it, nor around it.
        var builtIn = PluginOptions.BuiltInRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var install = options.InstallRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (install.StartsWith(builtIn, StringComparison.OrdinalIgnoreCase)
            || builtIn.StartsWith(install, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(nameof(PluginOptions.InstallPath), "must not be the build's own plugins folder, inside it or around it");
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>Builds the failure for one key, naming the environment variable that sets it.</summary>
    /// <param name="key">The offending property name.</param>
    /// <param name="problem">What is wrong with it.</param>
    /// <returns>The failure.</returns>
    private static ValidateOptionsResult Fail(string key, string problem)
    {
        return ValidateOptionsResult.Fail(
            $"{PluginOptions.SectionName}:{key} {problem} (set {PluginOptions.SectionName}__{key}).");
    }
}
