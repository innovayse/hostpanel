using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// What <see cref="PluginSshAccessService.IssueAsync"/> answered: the access, or why the host refused. Exactly one of the
/// two is set; only the factories build it. A refusal is a value, not an exception, inside the host; the host API turns
/// it into the SDK's <see cref="PluginHostAccessException"/> at the plugin boundary.
/// </summary>
public sealed class PluginSshIssueOutcome
{
    /// <summary>The issued access, carrying the private key once; <see langword="null"/> on a refusal.</summary>
    public PluginSshAccess? Access { get; }

    /// <summary>Why the host refused; <see langword="null"/> when the access was issued.</summary>
    public PluginHostAccessFailure? Failure { get; }

    /// <summary>Builds an outcome; only the factories call it.</summary>
    /// <param name="access">The issued access, or null.</param>
    /// <param name="failure">The refusal, or null.</param>
    private PluginSshIssueOutcome(PluginSshAccess? access, PluginHostAccessFailure? failure)
    {
        Access = access;
        Failure = failure;
    }

    /// <summary>The access was issued.</summary>
    /// <param name="access">Where to connect and the private key.</param>
    /// <returns>A successful outcome.</returns>
    public static PluginSshIssueOutcome Issued(PluginSshAccess access)
    {
        return new PluginSshIssueOutcome(access, null);
    }

    /// <summary>The host refused.</summary>
    /// <param name="failure">Why.</param>
    /// <returns>A refused outcome.</returns>
    public static PluginSshIssueOutcome Refused(PluginHostAccessFailure failure)
    {
        return new PluginSshIssueOutcome(null, failure);
    }

    /// <summary>Describes the outcome without the private key, so logging it never leaks the key.</summary>
    /// <returns>The key id or the refusal.</returns>
    public override string ToString()
    {
        return Access is { } access ? $"Issued {access.KeyId}" : $"Refused {Failure}";
    }
}
