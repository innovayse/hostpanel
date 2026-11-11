using System.Security.Cryptography;
using System.Text;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one definition of what this installation signs when it asks the plugin registry for a restricted plugin, and of the
/// headers that carry the result. The registry rebuilds exactly these bytes from the request it receives, so a single
/// differing character makes the signature fail: the version tag, the method, the request target as sent (path and query),
/// the SHA-256 of the body in lower-case hex, the timestamp and the nonce, one per line, joined with a line feed and
/// without a trailing one. The version tag keeps the key from being used to sign anything else.
/// </summary>
public static class RegistryCallSigning
{
    /// <summary>The first line of every signed text.</summary>
    public const string Version = "HOSTPANEL-REGISTRY-V1";

    /// <summary>The header that carries the instance id.</summary>
    public const string InstanceHeader = "X-Hostpanel-Instance";

    /// <summary>The header that carries the signing moment, in unix seconds.</summary>
    public const string TimestampHeader = "X-Hostpanel-Timestamp";

    /// <summary>The header that carries the nonce.</summary>
    public const string NonceHeader = "X-Hostpanel-Nonce";

    /// <summary>The header that carries the base64 signature.</summary>
    public const string SignatureHeader = "X-Hostpanel-Signature";

    /// <summary>The shortest nonce the registry accepts, in characters.</summary>
    public const int NonceMinLength = 16;

    /// <summary>How many random bytes make a nonce; base64 of them is 24 characters, inside the accepted 16 to 64.</summary>
    public const int NonceBytes = 18;

    /// <summary>The body hash of a call with no body, which is every call this installation signs: the SHA-256 of the empty string, in lower-case hex.</summary>
    public static readonly string EmptyBodySha256 = Convert.ToHexStringLower(SHA256.HashData([]));

    /// <summary>Builds the text a request is signed over.</summary>
    /// <param name="method">The HTTP method, upper case.</param>
    /// <param name="target">The request target exactly as it is sent: the path and the query, including <c>?ticket=</c> on a package download.</param>
    /// <param name="bodySha256">The SHA-256 of the body in lower-case hex; <see cref="EmptyBodySha256"/> for a call with no body.</param>
    /// <param name="timestamp">The timestamp header text, unix seconds.</param>
    /// <param name="nonce">The nonce header text.</param>
    /// <returns>The UTF-8 bytes to sign.</returns>
    public static byte[] Payload(string method, string target, string bodySha256, string timestamp, string nonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentException.ThrowIfNullOrEmpty(bodySha256);
        ArgumentException.ThrowIfNullOrEmpty(timestamp);
        ArgumentException.ThrowIfNullOrEmpty(nonce);

        return Encoding.UTF8.GetBytes(string.Join('\n', Version, method, target, bodySha256, timestamp, nonce));
    }

    /// <summary>Makes a new nonce: random bytes in base64, different on every call.</summary>
    /// <returns>A nonce of 24 characters.</returns>
    public static string NewNonce()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceBytes));
    }
}
