using System.Text.Encodings.Web;
using System.Text.Json;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The detached signature of a plugin package, <c>plugin.sig</c> at the root of the package: a small JSON object
/// <c>{"keyId":"...","signature":"..."}</c> where <c>signature</c> is the base64 of the 64-byte Ed25519 signature
/// over <see cref="PluginSignaturePayload"/>. The file is the one place that format is read and written.
/// </summary>
/// <param name="KeyId">The id of the key that signed, which names one of the trusted keys.</param>
/// <param name="Signature">The 64-byte Ed25519 signature.</param>
public sealed record PluginSignatureFile(string KeyId, byte[] Signature)
{
    /// <summary>The file name at the root of a package. The signature covers every other file, never this one.</summary>
    public const string FileName = "plugin.sig";

    /// <summary>The length of an Ed25519 signature in bytes.</summary>
    public const int SignatureLength = 64;

    /// <summary>The largest <c>plugin.sig</c> that is read: a real one is about 150 bytes.</summary>
    public const int MaxFileBytes = 4096;

    /// <summary>The member that holds the key id.</summary>
    private const string KeyIdMember = "keyId";

    /// <summary>The member that holds the base64 signature.</summary>
    private const string SignatureMember = "signature";

    /// <summary>Writes the base64 as it is (a <c>+</c> stays a <c>+</c>), which is plain ASCII and safe in a file that is never embedded in HTML.</summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Writes the file's JSON text.</summary>
    /// <returns>The text to store in <c>plugin.sig</c>.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [KeyIdMember] = KeyId,
            [SignatureMember] = Convert.ToBase64String(Signature),
        }, WriteOptions) + "\n";
    }

    /// <summary>Reads the file's JSON text.</summary>
    /// <param name="json">The text of <c>plugin.sig</c>.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="FormatException">
    /// The text is not a signature file: not JSON, a member missing or of the wrong kind, the key id not a valid
    /// key id, or the signature not 64 bytes of base64.
    /// </exception>
    public static PluginSignatureFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(KeyIdMember, out var keyId) || keyId.ValueKind != JsonValueKind.String
                || !root.TryGetProperty(SignatureMember, out var signature) || signature.ValueKind != JsonValueKind.String)
            {
                throw new FormatException($"plugin.sig must be an object with a string '{KeyIdMember}' and a string '{SignatureMember}'.");
            }

            var id = keyId.GetString();
            if (!TrustedKeyId.IsValid(id))
            {
                throw new FormatException("plugin.sig names a key id that is not a valid key id.");
            }

            var bytes = Convert.FromBase64String(signature.GetString()!);
            if (bytes.Length != SignatureLength)
            {
                throw new FormatException($"plugin.sig holds {bytes.Length} signature bytes, not {SignatureLength}.");
            }

            return new PluginSignatureFile(id!, bytes);
        }
        catch (JsonException ex)
        {
            throw new FormatException("plugin.sig is not valid JSON.", ex);
        }
    }
}
