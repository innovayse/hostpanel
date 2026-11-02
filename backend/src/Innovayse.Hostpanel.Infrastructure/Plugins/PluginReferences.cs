using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// <see cref="IPluginReferences"/> as a keyed hash: the reference is the HMAC-SHA256 of the plugin id and the host id, under
/// a key the host derives from its encryption key and a purpose label of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a hash and not a stored mapping.</b> A reference has to be stable (the same on every call and every
/// restart), different for every plugin, and impossible to turn back into the host id. A keyed hash is all three
/// with nothing to store, migrate or clean up: the plugin does not hold the key, so it can neither compute the
/// reference of an id it guesses nor recover an id from a reference, and two plugins get unrelated references for
/// the same client because the plugin id is part of the input. A mapping table would add a write on the first call of
/// every client and plugin, a place the host has to keep and back up, and a second source of truth.
/// </para>
/// <para>
/// <b>The key.</b> It is derived with HKDF-SHA256 from the encryption key the host already requires in every
/// environment, with the purpose label <see cref="Purpose"/>, so it is a different key from the one that encrypts
/// the secret settings and no new secret is added to a deployment. The price is stated: references are stable for as
/// long as the encryption key is, and a deployment that rotates it (which already means re-encrypting every secret)
/// gives every plugin new references, so a plugin keeping data under a reference loses sight of it. The label carries
/// a version (<c>-v1</c>) so a change of derivation is a new label rather than a silent change.
/// </para>
/// <para>
/// The input is <c>kind LF pluginId LF subject</c>. A plugin id is lower-case letters, digits and hyphens, so it
/// cannot contain the separator, and the subject, which may be anything, comes last; a client reference and a user
/// reference can never be equal because the kind is part of the input.
/// </para>
/// </remarks>
public sealed class PluginReferences : IPluginReferences
{
    /// <summary>Purpose label of the derived key; a change of derivation is a new label.</summary>
    public const string Purpose = "innovayse-hostpanel-plugin-references-v1";

    /// <summary>The kind part of a client reference input.</summary>
    private const string ClientKind = "client";

    /// <summary>The kind part of a user reference input.</summary>
    private const string UserKind = "user";

    /// <summary>The derived key, 32 bytes.</summary>
    private readonly byte[] _key;

    /// <summary>Initializes a new instance of the <see cref="PluginReferences"/> class.</summary>
    /// <param name="base64EncryptionKey">The host's Base64 encryption key, which the derived key comes from.</param>
    public PluginReferences(string base64EncryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64EncryptionKey);

        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, Convert.FromBase64String(base64EncryptionKey), 32, salt: null, info: Encoding.UTF8.GetBytes(Purpose));
    }

    /// <inheritdoc/>
    public string ForClient(string pluginId, int clientId)
    {
        return Compute(ClientKind, pluginId, clientId.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public string ForUser(string pluginId, string userId)
    {
        return Compute(UserKind, pluginId, userId);
    }

    /// <summary>Hashes one subject for one plugin.</summary>
    /// <param name="kind">Whether the subject is a client or a user.</param>
    /// <param name="pluginId">The plugin the reference is for.</param>
    /// <param name="subject">The host's id of the subject.</param>
    /// <returns>The 43 characters of the URL-safe Base64 of the hash.</returns>
    private string Compute(string kind, string pluginId, string subject)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{kind}\n{pluginId}\n{subject}"));
        return Base64Url.EncodeToString(hash);
    }
}
