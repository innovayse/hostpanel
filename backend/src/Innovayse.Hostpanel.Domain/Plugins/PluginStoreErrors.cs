namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// Refusal codes the <see cref="PluginDataEntry"/> aggregate throws as <see cref="DomainRuleException"/> when a plugin
/// breaks the store contract.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may not reference
/// Application; <c>HostpanelErrorCodes</c> re-declares the same values so the resource coverage test still requires a
/// sentence for each. The call runner answers a plugin's throw as <c>PLUGIN_FAILED</c>, so these codes are for the log.
/// </remarks>
public static class PluginStoreErrors
{
    /// <summary>A store key is empty, longer than <see cref="PluginDataEntry.MaxKeyLength"/> or outside the allowed characters.</summary>
    public const string KeyInvalid = "PLUGIN_STORE_KEY_INVALID";

    /// <summary>A store value is missing or longer than <see cref="PluginDataEntry.MaxValueLength"/> characters.</summary>
    public const string ValueInvalid = "PLUGIN_STORE_VALUE_INVALID";

    /// <summary>A new key would take a store scope past its configured number of keys.</summary>
    public const string KeyLimitReached = "PLUGIN_STORE_KEY_LIMIT_REACHED";
}
