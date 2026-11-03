using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Innovayse.Hostpanel.Application.Notifications.Common;

/*
 * Why the key exists: every mail sent from a handler reached through a durable queue can run more than
 * once (a retry replays the whole chain, and a recovered envelope runs again after a crash), so the
 * same message must map to the same key every time it is handled. The key is derived only from what the
 * message carries, never from a clock or a random value, and each part is length-prefixed before
 * hashing so two different part lists can never produce the same input.
 */

/// <summary>Builds the SHA-256 idempotency keys that make a mail send at most once per message and recipient.</summary>
public static class EmailIdempotencyKey
{
    /// <summary>Separator written after each length-prefixed part; never part of the length itself.</summary>
    private const char PartTerminator = '\n';

    /// <summary>Separator between a part's length and its text.</summary>
    private const char LengthSeparator = ':';

    /// <summary>
    /// The key for one mail sent in reaction to <paramref name="message"/>: the message's runtime type,
    /// its JSON form and the template slug, so two templates sent for one event get two keys.
    /// </summary>
    /// <param name="message">The event or command the mail reacts to; serialized with its runtime type.</param>
    /// <param name="templateSlug">The template the mail is rendered from.</param>
    /// <returns>64 lower-case hexadecimal characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="templateSlug"/> is null.</exception>
    public static string ForMessage(object message, string templateSlug)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(templateSlug);
        var type = message.GetType();
        return ForParts(type.FullName ?? type.Name, JsonSerializer.Serialize(message, type), templateSlug);
    }

    /// <summary>The key for a mail named by the caller's own parts, such as an invoice id and a threshold.</summary>
    /// <param name="parts">The values that together identify the mail; order matters.</param>
    /// <returns>64 lower-case hexadecimal characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> or one of its entries is null.</exception>
    public static string ForParts(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var input = new StringBuilder();
        foreach (var part in parts)
        {
            ArgumentNullException.ThrowIfNull(part);
            input.Append(part.Length.ToString(CultureInfo.InvariantCulture))
                .Append(LengthSeparator)
                .Append(part)
                .Append(PartTerminator);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString())));
    }

    /// <summary>
    /// The key for one recipient of a keyed mail: the main recipient and each contact copy are
    /// separate deliveries, so each gets its own key under the message's key.
    /// </summary>
    /// <param name="key">The message key from <see cref="ForMessage"/> or <see cref="ForParts"/>.</param>
    /// <param name="address">The recipient address; trimmed and lower-cased, so case never sends twice.</param>
    /// <returns>64 lower-case hexadecimal characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="address"/> is null.</exception>
    public static string ForRecipient(string key, string address)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(address);
        return ForParts(key, address.Trim().ToLowerInvariant());
    }
}
