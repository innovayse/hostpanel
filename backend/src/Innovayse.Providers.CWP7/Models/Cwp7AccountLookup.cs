using System.Text.Json;
using Innovayse.Providers.CWP7.Models.Enums;
using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.Providers.CWP7.Models;

/// <summary>The answer to "does this account exist, and in what state".</summary>
/// <param name="Outcome">Whether the account was found, is known to be absent, or is not known.</param>
/// <param name="Account">The account's details when <paramref name="Outcome"/> is <see cref="Cwp7LookupOutcome.Found"/>; otherwise null.</param>
internal sealed record Cwp7AccountLookup(Cwp7LookupOutcome Outcome, Cwp7AccountDetail? Account)
{
    /// <summary>The answer for a response that said nothing usable about the account.</summary>
    public static Cwp7AccountLookup Unknown { get; } = new(Cwp7LookupOutcome.Unknown, null);

    /// <summary>The answer for a successful response that holds no such account.</summary>
    public static Cwp7AccountLookup NotFound { get; } = new(Cwp7LookupOutcome.NotFound, null);

    /// <summary>Reads an account-detail response body.</summary>
    /// <remarks>
    /// The success body carries the account under <c>msj</c>, either as one object or as a list
    /// of objects. Only a status of "OK" with a parseable payload is trusted: a refusal or an
    /// error page is <see cref="Unknown"/>, never "not found", so a failed lookup cannot make a
    /// caller believe an account is gone.
    /// </remarks>
    /// <param name="raw">The response body.</param>
    /// <param name="username">The account that was asked about.</param>
    /// <returns>The lookup answer.</returns>
    public static Cwp7AccountLookup FromBody(string raw, string username)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind is not JsonValueKind.Object
                || !root.TryGetProperty("status", out var status)
                || status.ValueKind is not JsonValueKind.String
                || !string.Equals(status.GetString(), HostingPanelApiWire.OkStatus, StringComparison.OrdinalIgnoreCase)
                || !root.TryGetProperty("msj", out var payload))
            {
                return Unknown;
            }

            return payload.ValueKind switch
            {
                JsonValueKind.Object => Match(payload, username),
                JsonValueKind.Array => MatchAny(payload, username),
                _ => Unknown,
            };
        }
        catch (JsonException)
        {
            return Unknown;
        }
    }

    /// <summary>Matches one payload object against the asked username.</summary>
    /// <param name="entry">The payload object.</param>
    /// <param name="username">The account that was asked about.</param>
    /// <returns>Found when the entry is that account, otherwise not found.</returns>
    private static Cwp7AccountLookup Match(JsonElement entry, string username)
    {
        var user = ReadString(entry, "user");
        if (!string.Equals(user, username, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound;
        }

        var suspended = entry.TryGetProperty("suspended", out var flag) && HostingPanelApiReader.IsFlagSet(flag);
        return new Cwp7AccountLookup(
            Cwp7LookupOutcome.Found,
            new Cwp7AccountDetail(user!, ReadString(entry, "domain") ?? string.Empty, suspended));
    }

    /// <summary>Matches a list payload against the asked username.</summary>
    /// <param name="entries">The payload list.</param>
    /// <param name="username">The account that was asked about.</param>
    /// <returns>Found when any entry is that account, otherwise not found.</returns>
    private static Cwp7AccountLookup MatchAny(JsonElement entries, string username)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            var match = Match(entry, username);
            if (match.Outcome is Cwp7LookupOutcome.Found)
            {
                return match;
            }
        }

        return NotFound;
    }

    /// <summary>Reads a string property, tolerating a missing or non-string value.</summary>
    /// <param name="entry">The JSON object holding the property.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The string, or <see langword="null"/> when absent or not a string.</returns>
    private static string? ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
}
