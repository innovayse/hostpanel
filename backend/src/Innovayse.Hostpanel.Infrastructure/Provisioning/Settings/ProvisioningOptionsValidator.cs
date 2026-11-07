using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;

/// <summary>Refuses startup when a cPanel port setting is not a TCP port.</summary>
/// <remarks>
/// Both ports build a URL for every cPanel server (the WHM address and the sign-in address shown to
/// account owners), so a value outside 1 to 65535 would fail on the first customer who needed
/// either one instead of when the deployment is started.
/// </remarks>
public sealed class ProvisioningOptionsValidator : IValidateOptions<ProvisioningOptions>
{
    /// <summary>The lowest TCP port number.</summary>
    public const int MinPort = 1;

    /// <summary>The highest TCP port number.</summary>
    public const int MaxPort = 65535;

    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming every offending key.</returns>
    public ValidateOptionsResult Validate(string? name, ProvisioningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        CheckPort(failures, nameof(ProvisioningOptions.CPanelWhmPort), options.CPanelWhmPort);
        CheckPort(failures, nameof(ProvisioningOptions.CPanelUserPort), options.CPanelUserPort);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Adds a failure naming the key when the port is out of range.</summary>
    /// <param name="failures">The failures collected so far.</param>
    /// <param name="key">The option's property name.</param>
    /// <param name="port">The configured port.</param>
    private static void CheckPort(List<string> failures, string key, int port)
    {
        if (port is < MinPort or > MaxPort)
        {
            failures.Add(
                $"{ProvisioningOptions.SectionName}:{key} must be between {MinPort} and {MaxPort} "
                + $"(set {ProvisioningOptions.SectionName}__{key}).");
        }
    }
}
