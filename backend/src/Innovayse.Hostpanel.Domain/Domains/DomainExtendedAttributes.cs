using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// The extra values a registry asks for beside the standard contact, keyed by the field keys the registrar plugin's manifest
/// declares. The domain never reads a key or a value: they are kept as given and handed to the plugin unchanged, so what a field
/// means is the plugin's business. A blank value is no value and is left out, so an optional field the person did not fill in
/// is never stored or sent.
/// </summary>
public sealed class DomainExtendedAttributes : ValueObject
{
    /// <summary>
    /// The longest stored form (the JSON text), in characters. The column that holds it is sized from this figure, so a set
    /// of values that fits here always fits the column once it is encrypted.
    /// </summary>
    public const int MaxSerializedLength = 8000;

    /// <summary>The JSON writer's settings: text is kept readable instead of escaped, which keeps the stored form short.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The values by key, in key order so the same values always serialise to the same text.</summary>
    private readonly SortedDictionary<string, string> _values;

    /// <summary>Initializes a new instance of the <see cref="DomainExtendedAttributes"/> class.</summary>
    /// <param name="values">The values by key, already free of blank ones.</param>
    private DomainExtendedAttributes(SortedDictionary<string, string> values)
    {
        _values = values;
    }

    /// <summary>Gets the value set that holds nothing.</summary>
    public static DomainExtendedAttributes Empty { get; } = new([]);

    /// <summary>Gets the values by key.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Gets a value indicating whether no value is held.</summary>
    public bool IsEmpty => _values.Count == 0;

    /// <summary>Builds the value set from the values a caller gave, leaving out the blank ones.</summary>
    /// <param name="values">The values by key, or <see langword="null"/> for none.</param>
    /// <param name="attributes">The value set, when the values fit <see cref="MaxSerializedLength"/>.</param>
    /// <returns><see langword="true"/> when the values fit; <see langword="false"/> when their stored form would be too long.</returns>
    public static bool TryCreate(IReadOnlyDictionary<string, string>? values, [NotNullWhen(true)] out DomainExtendedAttributes? attributes)
    {
        SortedDictionary<string, string> kept = new(StringComparer.Ordinal);
        foreach (var (key, value) in values ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                kept[key] = value;
            }
        }

        var candidate = new DomainExtendedAttributes(kept);
        attributes = candidate.Serialize()?.Length > MaxSerializedLength ? null : candidate;
        return attributes is not null;
    }

    /// <summary>Builds the value set from values already checked against <see cref="MaxSerializedLength"/>.</summary>
    /// <param name="values">The values by key, or <see langword="null"/> for none.</param>
    /// <returns>The value set.</returns>
    /// <exception cref="DomainRuleException">Thrown when the stored form of the values would be longer than <see cref="MaxSerializedLength"/>.</exception>
    public static DomainExtendedAttributes From(IReadOnlyDictionary<string, string>? values)
    {
        if (!TryCreate(values, out var attributes))
        {
            throw new DomainRuleException(
                DomainsErrors.DomainFieldInvalid,
                $"The extended attributes of a domain take at most {MaxSerializedLength} characters once stored.");
        }

        return attributes;
    }

    /// <summary>Reads the value set back from its stored form.</summary>
    /// <param name="json">The stored JSON text, or <see langword="null"/> for none.</param>
    /// <returns>The value set; empty when nothing is stored.</returns>
    public static DomainExtendedAttributes Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Empty;
        }

        return new DomainExtendedAttributes(
            new SortedDictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [],
                StringComparer.Ordinal));
    }

    /// <summary>Writes the value set in the form it is stored in.</summary>
    /// <returns>A JSON object of the values by key, or <see langword="null"/> when none is held.</returns>
    public string? Serialize()
    {
        return IsEmpty ? null : JsonSerializer.Serialize(_values, JsonOptions);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var (key, value) in _values)
        {
            yield return key;
            yield return value;
        }
    }
}
