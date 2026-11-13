using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores <see cref="SshProviderFailure"/> in <c>SshKeys.LastSyncError</c> as the exact lowercase
/// code that column already holds, so rows written before the enum existed read back unchanged and
/// the column type and length stay as they are.
/// </summary>
/// <remarks>
/// The stored forms are a persistence contract: never rename one. A value read from the column
/// that is not in the table becomes <see cref="SshProviderFailure.Unknown"/>.
/// </remarks>
public sealed class SshProviderFailureConverter : ValueConverter<SshProviderFailure, string>
{
    /// <summary>The stored form of every failure.</summary>
    private static readonly IReadOnlyDictionary<SshProviderFailure, string> Stored =
        new Dictionary<SshProviderFailure, string>
        {
            [SshProviderFailure.Unknown] = "unknown",
            [SshProviderFailure.Unreachable] = "unreachable",
            [SshProviderFailure.Timeout] = "timeout",
            [SshProviderFailure.InternalError] = "internal_error",
            [SshProviderFailure.UnsupportedVersion] = "unsupported_version",
            [SshProviderFailure.InvalidRequest] = "invalid_request",
            [SshProviderFailure.UnknownOperation] = "unknown_operation",
            [SshProviderFailure.RequestTooLarge] = "request_too_large",
            [SshProviderFailure.InvalidAccount] = "invalid_account",
            [SshProviderFailure.AccountNotFound] = "account_not_found",
            [SshProviderFailure.AccountNotEligible] = "account_not_eligible",
            [SshProviderFailure.SshDisabled] = "ssh_disabled",
            [SshProviderFailure.InvalidKey] = "invalid_key",
            [SshProviderFailure.DuplicateKey] = "duplicate_key",
            [SshProviderFailure.KeyIdConflict] = "key_id_conflict",
            [SshProviderFailure.TooManyKeys] = "too_many_keys",
            [SshProviderFailure.UnsafePath] = "unsafe_path",
            [SshProviderFailure.FileTooLarge] = "file_too_large",
            [SshProviderFailure.AccountSuspended] = "account_suspended",
            [SshProviderFailure.AgentNotConfigured] = "agent_not_configured",
            [SshProviderFailure.BindingMismatch] = "binding_mismatch",
            [SshProviderFailure.NotSupported] = "not_supported",
            [SshProviderFailure.SiteNotOwned] = "site_not_owned",
            [SshProviderFailure.SitePrepareFailed] = "site_prepare_failed",
        };

    /// <summary>The failure each stored form reads back as.</summary>
    private static readonly IReadOnlyDictionary<string, SshProviderFailure> Read =
        Stored.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Initialises the converter.</summary>
    public SshProviderFailureConverter()
        : base(failure => ToStored(failure), value => FromStored(value))
    {
    }

    /// <summary>Gets the stored form of a failure.</summary>
    /// <param name="failure">The failure to store.</param>
    /// <returns>The column value.</returns>
    public static string ToStored(SshProviderFailure failure) => Stored[failure];

    /// <summary>Reads a stored value back.</summary>
    /// <param name="value">The column value.</param>
    /// <returns>The failure, or <see cref="SshProviderFailure.Unknown"/> for a value not in the table.</returns>
    public static SshProviderFailure FromStored(string value) =>
        Read.TryGetValue(value, out var failure) ? failure : SshProviderFailure.Unknown;
}
