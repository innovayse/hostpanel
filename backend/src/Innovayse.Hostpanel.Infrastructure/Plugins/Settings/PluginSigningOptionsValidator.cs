using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>Refuses startup when a trusted plugin signing key is not usable, so a typo cannot silently leave a key out.</summary>
public sealed class PluginSigningOptionsValidator : IValidateOptions<PluginSigningOptions>
{
    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming the offending key.</returns>
    public ValidateOptionsResult Validate(string? name, PluginSigningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in options.TrustedKeys)
        {
            if (key is null || !TrustedKeyId.IsValid(key.KeyId))
            {
                return Fail("KeyId", "must be 1 to 64 characters of a-z, 0-9, dot, hyphen and underscore, starting with a letter or digit");
            }

            if (!key.TryDecode(out _))
            {
                return Fail("PublicKey", $"of '{key.KeyId}' must be the base64 of a 32-byte Ed25519 public key");
            }

            if (!seen.Add(key.KeyId))
            {
                return Fail("KeyId", $"'{key.KeyId}' is listed more than once");
            }
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>Builds the failure for one member of a key entry.</summary>
    /// <param name="member">The offending member name.</param>
    /// <param name="problem">What is wrong with it.</param>
    /// <returns>The failure.</returns>
    private static ValidateOptionsResult Fail(string member, string problem)
    {
        return ValidateOptionsResult.Fail(
            $"{PluginSigningOptions.SectionName}:MaintainerKeys and :OperatorKeys: {member} {problem} "
            + $"(set {PluginSigningOptions.SectionName}__OperatorKeys__<n>__{member}).");
    }
}
