namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// Refusal codes the <see cref="PluginDataEntry"/> and <see cref="PluginDataScope"/> aggregates throw as
/// <see cref="DomainRuleException"/> when a plugin breaks the store contract or fills its scope.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may not reference
/// Application; <c>HostpanelErrorCodes</c> re-declares the same values so the resource coverage test still requires a
/// sentence for each. The call runner answers a plugin's throw as <c>PLUGIN_FAILED</c>, so the key and value codes are
/// for the log; the quota code is answered as itself, because the store raises the call's quota signal before it
/// rethrows.
/// </remarks>
public static class PluginStoreErrors
{
    /// <summary>A store key is empty, longer than <see cref="PluginDataEntry.MaxKeyLength"/> or outside the allowed characters.</summary>
    public const string KeyInvalid = "PLUGIN_STORE_KEY_INVALID";

    /// <summary>A store value is missing or longer than <see cref="PluginDataEntry.MaxValueLength"/> characters.</summary>
    public const string ValueInvalid = "PLUGIN_STORE_VALUE_INVALID";

    /// <summary>
    /// A write would take a store scope past its <see cref="PluginStoreQuota"/>: more keys than
    /// <see cref="PluginStoreQuota.MaxKeys"/> or more characters than <see cref="PluginStoreQuota.MaxChars"/>.
    /// </summary>
    public const string QuotaExceeded = "PLUGIN_STORE_QUOTA_EXCEEDED";
}
