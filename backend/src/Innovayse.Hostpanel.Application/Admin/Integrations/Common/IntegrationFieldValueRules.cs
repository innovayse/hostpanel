using System.Globalization;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Checks one submitted integration field value against the type its definition declares and returns the value
/// to store. The save handler asks it for every field before it writes any, so a value that does not fit is
/// refused instead of being stored as text for a reader to trip over later. A refusal names a resource key and its
/// arguments, never a sentence, so the handler words it in the request's language.
/// </summary>
/// <remarks>
/// Whether a field is required is not decided here: an empty value is accepted for every type, because a required
/// field is enforced when the integration is activated and must not block saving a half-configured or disabled one.
/// </remarks>
public static class IntegrationFieldValueRules
{
    /// <summary>Resource key of the sentence for a secret that must be non-blank.</summary>
    public const string ExpectSecret = "IntegrationFieldExpectSecret";

    /// <summary>Resource key of the sentence for an absolute http or https address.</summary>
    public const string ExpectUrl = "IntegrationFieldExpectUrl";

    /// <summary>Resource key of the sentence for a boolean.</summary>
    public const string ExpectToggle = "IntegrationFieldExpectToggle";

    /// <summary>Resource key of the sentence for one of the declared options; argument 0 is the joined options.</summary>
    public const string ExpectSelect = "IntegrationFieldExpectSelect";

    /// <summary>Resource key of the sentence for a bounded number; arguments are the lower and upper bound.</summary>
    public const string ExpectNumberRange = "IntegrationFieldExpectNumberRange";

    /// <summary>Resource key of the sentence for a number with a lower bound; argument 0 is the bound.</summary>
    public const string ExpectNumberMin = "IntegrationFieldExpectNumberMin";

    /// <summary>Resource key of the sentence for a number with an upper bound; argument 0 is the bound.</summary>
    public const string ExpectNumberMax = "IntegrationFieldExpectNumberMax";

    /// <summary>Resource key of the sentence for an unbounded number.</summary>
    public const string ExpectNumber = "IntegrationFieldExpectNumber";

    /// <summary>Resource key of the sentence for a type with no narrower rule.</summary>
    public const string ExpectValue = "IntegrationFieldExpectValue";

    /// <summary>Checks a value against its field definition.</summary>
    /// <param name="field">The field's declared definition.</param>
    /// <param name="value">The submitted value; <c>null</c> reads as empty.</param>
    /// <returns>
    /// A <see cref="IntegrationFieldValueCheck"/> carrying the canonical value to store when the value fits, or the
    /// resource key and arguments naming what the field accepts when it does not.
    /// </returns>
    public static IntegrationFieldValueCheck Check(FieldDefinitionDto field, string? value)
    {
        ArgumentNullException.ThrowIfNull(field);

        var text = value ?? string.Empty;

        // A secret keeps its value untouched; a blank one means "unchanged" and is never stored, so it is refused here.
        if (field.Type is IntegrationFieldType.Secret or IntegrationFieldType.Password)
        {
            return string.IsNullOrWhiteSpace(text)
                ? IntegrationFieldValueCheck.Refused(ExpectSecret)
                : IntegrationFieldValueCheck.Accepted(text);
        }

        if (field.Type is IntegrationFieldType.Text or IntegrationFieldType.TextArea)
        {
            return IntegrationFieldValueCheck.Accepted(text);
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            // An empty value clears the field, whatever the type or Required says: required is enforced at activation.
            return IntegrationFieldValueCheck.Accepted(string.Empty);
        }

        return field.Type switch
        {
            IntegrationFieldType.Url => CheckUrl(trimmed),
            IntegrationFieldType.Number => CheckNumber(field, trimmed),
            IntegrationFieldType.Select => CheckSelect(field, trimmed),
            IntegrationFieldType.Toggle => CheckToggle(trimmed),
            _ => Describe(field),
        };
    }

    /// <summary>Checks an absolute http or https address.</summary>
    /// <param name="value">The trimmed, non-empty value.</param>
    /// <returns>The outcome.</returns>
    private static IntegrationFieldValueCheck CheckUrl(string value)
    {
        var ok = Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        return ok
            ? IntegrationFieldValueCheck.Accepted(value)
            : IntegrationFieldValueCheck.Refused(ExpectUrl);
    }

    /// <summary>Checks an invariant-culture number inside the field's bounds.</summary>
    /// <param name="field">The field definition, carrying the bounds.</param>
    /// <param name="value">The trimmed, non-empty value.</param>
    /// <returns>The outcome.</returns>
    private static IntegrationFieldValueCheck CheckNumber(FieldDefinitionDto field, string value)
    {
        var parsed = decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var number);
        if (!parsed || (field.Min is { } min && number < min) || (field.Max is { } max && number > max))
        {
            return Describe(field);
        }

        return IntegrationFieldValueCheck.Accepted(value);
    }

    /// <summary>Checks that a value is one of the declared options and returns the declared spelling.</summary>
    /// <param name="field">The field definition, carrying the options.</param>
    /// <param name="value">The trimmed, non-empty value.</param>
    /// <returns>The outcome.</returns>
    private static IntegrationFieldValueCheck CheckSelect(FieldDefinitionDto field, string value)
    {
        var options = field.Options ?? [];
        var match = options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? Describe(field)
            : IntegrationFieldValueCheck.Accepted(match);
    }

    /// <summary>Checks a boolean and returns it lowercased.</summary>
    /// <param name="value">The trimmed, non-empty value.</param>
    /// <returns>The outcome.</returns>
    private static IntegrationFieldValueCheck CheckToggle(string value)
    {
        return bool.TryParse(value, out var flag)
            ? IntegrationFieldValueCheck.Accepted(flag ? "true" : "false")
            : IntegrationFieldValueCheck.Refused(ExpectToggle);
    }

    /// <summary>Builds the refusal naming what a field of this type accepts.</summary>
    /// <param name="field">The field definition.</param>
    /// <returns>The refusal, carrying the resource key and arguments of the expectation.</returns>
    private static IntegrationFieldValueCheck Describe(FieldDefinitionDto field)
    {
        return field.Type switch
        {
            IntegrationFieldType.Url => IntegrationFieldValueCheck.Refused(ExpectUrl),
            IntegrationFieldType.Toggle => IntegrationFieldValueCheck.Refused(ExpectToggle),
            IntegrationFieldType.Select => IntegrationFieldValueCheck.Refused(
                ExpectSelect, string.Join(", ", field.Options ?? [])),
            IntegrationFieldType.Number => (field.Min, field.Max) switch
            {
                ({ } lo, { } hi) => IntegrationFieldValueCheck.Refused(ExpectNumberRange, Format(lo), Format(hi)),
                ({ } lo, null) => IntegrationFieldValueCheck.Refused(ExpectNumberMin, Format(lo)),
                (null, { } hi) => IntegrationFieldValueCheck.Refused(ExpectNumberMax, Format(hi)),
                _ => IntegrationFieldValueCheck.Refused(ExpectNumber),
            },
            _ => IntegrationFieldValueCheck.Refused(ExpectValue),
        };
    }

    /// <summary>Writes a bound with an invariant decimal point.</summary>
    /// <param name="bound">The bound.</param>
    /// <returns>The text.</returns>
    private static string Format(decimal bound)
    {
        return bound.ToString(CultureInfo.InvariantCulture);
    }
}
