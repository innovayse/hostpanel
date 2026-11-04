using System.Globalization;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Checks one submitted integration field value against the type its definition declares and returns the value
/// to store. The save handler asks it for every field before it writes any, so a value that does not fit is
/// refused instead of being stored as text for a reader to trip over later.
/// </summary>
public static class IntegrationFieldValueRules
{
    /// <summary>Checks a value against its field definition.</summary>
    /// <param name="field">The field's declared definition.</param>
    /// <param name="value">The submitted value; <c>null</c> reads as empty.</param>
    /// <returns>
    /// A <see cref="IntegrationFieldValueCheck"/> carrying the canonical value to store when the value fits, or the
    /// "Expected: ..." sentence naming what the field accepts when it does not.
    /// </returns>
    public static IntegrationFieldValueCheck Check(FieldDefinitionDto field, string? value)
    {
        ArgumentNullException.ThrowIfNull(field);

        var text = value ?? string.Empty;

        // A secret keeps its value untouched; a blank one means "unchanged" and is never stored, so it is refused here.
        if (field.Type is IntegrationFieldType.Secret or IntegrationFieldType.Password)
        {
            return string.IsNullOrWhiteSpace(text)
                ? IntegrationFieldValueCheck.Refused("a non-blank value; leave the field out to keep the stored secret")
                : IntegrationFieldValueCheck.Accepted(text);
        }

        if (field.Type is IntegrationFieldType.Text or IntegrationFieldType.TextArea)
        {
            return IntegrationFieldValueCheck.Accepted(text);
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            // An empty value clears the field; a required one cannot be left empty by the save itself.
            return field.Required
                ? IntegrationFieldValueCheck.Refused(Describe(field))
                : IntegrationFieldValueCheck.Accepted(string.Empty);
        }

        return field.Type switch
        {
            IntegrationFieldType.Url => CheckUrl(trimmed),
            IntegrationFieldType.Number => CheckNumber(field, trimmed),
            IntegrationFieldType.Select => CheckSelect(field, trimmed),
            IntegrationFieldType.Toggle => CheckToggle(trimmed),
            _ => IntegrationFieldValueCheck.Refused(Describe(field)),
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
            : IntegrationFieldValueCheck.Refused("an absolute http or https address");
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
            return IntegrationFieldValueCheck.Refused(Describe(field));
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
            ? IntegrationFieldValueCheck.Refused(Describe(field))
            : IntegrationFieldValueCheck.Accepted(match);
    }

    /// <summary>Checks a boolean and returns it lowercased.</summary>
    /// <param name="value">The trimmed, non-empty value.</param>
    /// <returns>The outcome.</returns>
    private static IntegrationFieldValueCheck CheckToggle(string value)
    {
        return bool.TryParse(value, out var flag)
            ? IntegrationFieldValueCheck.Accepted(flag ? "true" : "false")
            : IntegrationFieldValueCheck.Refused("true or false");
    }

    /// <summary>Names what a field of this type accepts.</summary>
    /// <param name="field">The field definition.</param>
    /// <returns>The expectation, without the "Expected: " prefix.</returns>
    private static string Describe(FieldDefinitionDto field)
    {
        return field.Type switch
        {
            IntegrationFieldType.Url => "an absolute http or https address",
            IntegrationFieldType.Toggle => "true or false",
            IntegrationFieldType.Select => $"one of {string.Join(", ", field.Options ?? [])}",
            IntegrationFieldType.Number => (field.Min, field.Max) switch
            {
                ({ } lo, { } hi) => $"a number from {Format(lo)} to {Format(hi)}",
                ({ } lo, null) => $"a number of at least {Format(lo)}",
                (null, { } hi) => $"a number of at most {Format(hi)}",
                _ => "a number",
            },
            _ => "a non-empty value",
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
