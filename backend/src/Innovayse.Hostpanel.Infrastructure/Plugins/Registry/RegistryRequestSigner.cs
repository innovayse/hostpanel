using System.Globalization;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// <see cref="IRegistryRequestSigner"/> over the stored registry identity. It signs only while the identity is verified,
/// builds the text with <see cref="RegistryCallSigning"/> (the one definition the registry also follows), and keeps the private
/// key inside this class: a caller receives the four header values and nothing else.
/// </summary>
public sealed class RegistryRequestSigner : IRegistryRequestSigner
{
    /// <summary>Reads and writes the stored identity.</summary>
    private readonly RegistryInstanceStore _store;

    /// <summary>Signs the text.</summary>
    private readonly IRegistryInstanceKeys _keys;

    /// <summary>The clock the signing moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the signer.</summary>
    /// <param name="store">Reads and writes the stored identity.</param>
    /// <param name="keys">Signs the text.</param>
    /// <param name="clock">The clock the signing moment is read from.</param>
    public RegistryRequestSigner(RegistryInstanceStore store, IRegistryInstanceKeys keys, IClock clock)
    {
        _store = store;
        _keys = keys;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<RegistryRequestSignature>> SignAsync(string method, string target, CancellationToken ct)
    {
        var record = await _store.LoadAsync(ct);
        if (record is null || record.InstanceId is not { } instanceId || record.State != RegistryInstanceState.Verified)
        {
            return Result<RegistryRequestSignature>.FailureCode(
                record?.State == RegistryInstanceState.Revoked
                    ? HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED
                    : HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED);
        }

        var timestamp = _clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = RegistryCallSigning.NewNonce();
        var payload = RegistryCallSigning.Payload(method, target, RegistryCallSigning.EmptyBodySha256, timestamp, nonce);
        return Result<RegistryRequestSignature>.Success(
            new RegistryRequestSignature(instanceId, timestamp, nonce, _keys.Sign(record.PrivateKey, payload)));
    }

    /// <inheritdoc/>
    public async Task ReportRefusalAsync(string errorCode, CancellationToken ct)
    {
        var record = await _store.LoadAsync(ct);
        if (record is null)
        {
            return;
        }

        if (errorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED && record.State != RegistryInstanceState.Revoked)
        {
            record.Revoke();
        }
        else if (errorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED && record.State == RegistryInstanceState.Verified)
        {
            record.Unverify(errorCode);
        }
        else
        {
            return;
        }

        await _store.SaveAsync(record, ct);
    }
}
