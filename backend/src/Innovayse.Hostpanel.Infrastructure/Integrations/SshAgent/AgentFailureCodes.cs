using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// Maps the <c>error</c> codes of the agent contract (v1) to <see cref="SshProviderFailure"/>.
/// The agent's wire codes stop here; nothing past the adapter sees them.
/// </summary>
internal static class AgentFailureCodes
{
    /// <summary>Every code the agent contract defines, with the failure it means.</summary>
    private static readonly IReadOnlyDictionary<string, SshProviderFailure> Map =
        new Dictionary<string, SshProviderFailure>(StringComparer.Ordinal)
        {
            ["timeout"] = SshProviderFailure.Timeout,
            ["internal_error"] = SshProviderFailure.InternalError,
            ["unsupported_version"] = SshProviderFailure.UnsupportedVersion,
            ["invalid_request"] = SshProviderFailure.InvalidRequest,
            ["unknown_operation"] = SshProviderFailure.UnknownOperation,
            ["request_too_large"] = SshProviderFailure.RequestTooLarge,
            ["invalid_account"] = SshProviderFailure.InvalidAccount,
            ["account_not_found"] = SshProviderFailure.AccountNotFound,
            ["account_not_eligible"] = SshProviderFailure.AccountNotEligible,
            ["ssh_disabled"] = SshProviderFailure.SshDisabled,
            ["invalid_key"] = SshProviderFailure.InvalidKey,
            ["duplicate_key"] = SshProviderFailure.DuplicateKey,
            ["key_id_conflict"] = SshProviderFailure.KeyIdConflict,
            ["too_many_keys"] = SshProviderFailure.TooManyKeys,
            ["unsafe_path"] = SshProviderFailure.UnsafePath,
            ["file_too_large"] = SshProviderFailure.FileTooLarge,
            ["account_suspended"] = SshProviderFailure.AccountSuspended,
            ["not_supported"] = SshProviderFailure.NotSupported,
            ["site_not_owned"] = SshProviderFailure.SiteNotOwned,
            ["site_prepare_failed"] = SshProviderFailure.SitePrepareFailed,
        };

    /// <summary>Translates an agent error code.</summary>
    /// <param name="code">The agent's <c>error</c> value.</param>
    /// <returns>The matching failure, or <see cref="SshProviderFailure.Unknown"/> for a code this version does not know.</returns>
    public static SshProviderFailure Parse(string code) =>
        Map.TryGetValue(code, out var failure) ? failure : SshProviderFailure.Unknown;
}
