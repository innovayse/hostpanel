using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>Mapping helpers for <see cref="SshKey"/>.</summary>
public static class SshKeyExtensions
{
    /// <summary>Maps a key to its portal DTO.</summary>
    /// <param name="key">The stored key record.</param>
    /// <param name="context">The resolved service context, used to tell whether the key is stale.</param>
    /// <param name="presentOnServer">What the server reported, if it was asked.</param>
    /// <returns>The DTO.</returns>
    public static SshKeyDto ToDto(this SshKey key, SshServiceContext context, bool? presentOnServer = null) =>
        new(
            key.KeyId,
            key.Label,
            key.Fingerprint,
            key.AllowedFrom,
            key.CreatedAt,
            key.SyncState,
            !key.IsBoundTo(context.Service.ServerId, context.Service.ProvisioningRef),
            presentOnServer);
}
