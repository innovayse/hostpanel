using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>One SSH key as the portal lists it. Never carries key material beyond the fingerprint.</summary>
/// <param name="KeyId">Stable public id, used in the revoke route.</param>
/// <param name="Label">Friendly label.</param>
/// <param name="Fingerprint">SHA256 fingerprint.</param>
/// <param name="AllowedFrom">
/// The addresses and CIDR networks the key may be used from; empty for a key that works from anywhere.
/// </param>
/// <param name="CreatedAt">When it was added.</param>
/// <param name="SyncState">Last state observed on the server.</param>
/// <param name="IsStale">
/// Whether the service has since moved to another server or account. A stale key is not managed
/// from the portal; support removes it from the old server.
/// </param>
/// <param name="PresentOnServer">
/// Whether the server reported the key on the last listing; <see langword="null"/> when the server
/// was not asked or did not answer.
/// </param>
public sealed record SshKeyDto(
    Guid KeyId,
    string Label,
    string Fingerprint,
    IReadOnlyList<string> AllowedFrom,
    DateTimeOffset CreatedAt,
    SshKeySyncState SyncState,
    bool IsStale,
    bool? PresentOnServer);
