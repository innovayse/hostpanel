using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Application.Common.Validators;

/// <summary>
/// Rule chains that many validators repeat, named once. A validator states <em>what</em> a property
/// must be (<c>MustBePositiveId</c>, <c>MustBePage</c>) and the limits behind the name live here.
/// </summary>
public static class ValidationRuleExtensions
{
    /// <summary>Requires an identifier to be a positive integer.</summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <param name="rule">The rule builder for the id property.</param>
    /// <param name="message">The localized sentence reported when the id is not positive.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int> MustBePositiveId<T>(this IRuleBuilder<T, int> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.GreaterThan(0).WithMessage(message);
    }

    /// <summary>
    /// Requires an ISO 4217 alpha currency code in the exact shape the domain stores: three ASCII
    /// letters, with no padding (see <see cref="CurrencyCode.IsValid"/>). An absent, empty, padded,
    /// numeric or punctuated value is refused with the one message, so a validator needs no separate
    /// not-empty or length rule.
    /// </summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <typeparam name="TProperty">The type of the property, a <see cref="string"/> (nullable or not).</typeparam>
    /// <param name="rule">The rule builder for the currency property.</param>
    /// <param name="message">The localized sentence reported when the value is not a currency code.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> MustBeCurrencyCode<T, TProperty>(
        this IRuleBuilder<T, TProperty> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(value => CurrencyCode.IsValid(value as string)).WithMessage(message);
    }

    /// <summary>Requires a 1-based page number.</summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <param name="rule">The rule builder for the page property.</param>
    /// <param name="message">The localized sentence reported when the page is below 1.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int> MustBePage<T>(this IRuleBuilder<T, int> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.GreaterThanOrEqualTo(1).WithMessage(message);
    }

    /// <summary>Requires a page size between 1 and the largest the use case allows.</summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <param name="rule">The rule builder for the page-size property.</param>
    /// <param name="maxPageSize">The largest page size this use case accepts.</param>
    /// <param name="message">The localized sentence reported when the size is out of range.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int> MustBePageSize<T>(
        this IRuleBuilder<T, int> rule, int maxPageSize, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.InclusiveBetween(1, maxPageSize).WithMessage(message);
    }

    /// <summary>
    /// Requires a link that is safe to render in an <c>href</c> or <c>src</c>: an absolute <c>http</c> or
    /// <c>https</c> address, or a root-relative path. Anything else (<c>javascript:</c>, <c>data:</c>,
    /// protocol-relative <c>//host</c>) is refused. An absent or empty value passes: whether a link is
    /// required is a separate rule.
    /// </summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <param name="rule">The rule builder for the link property.</param>
    /// <param name="message">The localized sentence reported when the link has another shape.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeWebLink<T>(this IRuleBuilder<T, string?> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(IsWebLink).WithMessage(message);
    }

    /// <summary>
    /// Requires an absolute <c>http</c> or <c>https</c> address, the only shape a return URL can have.
    /// Which hosts are acceptable is the handler's answer; this rule refuses only values that are not
    /// web addresses at all (<c>javascript:</c>, <c>data:</c>, relative paths).
    /// </summary>
    /// <typeparam name="T">The command or query being validated.</typeparam>
    /// <param name="rule">The rule builder for the URL property.</param>
    /// <param name="message">The localized sentence reported when the value is not an absolute web address.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeAbsoluteWebUrl<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(IsAbsoluteWebUrl).WithMessage(message);
    }
    /// <summary>Requires a reorder request to name no more than <see cref="SortOrderLimits.MaxItems"/> rows.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the items property.</param>
    /// <param name="message">The localized sentence reported when the request names too many rows.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, List<SortOrderItem>> MustNotExceedSortOrderLimit<T>(
        this IRuleBuilder<T, List<SortOrderItem>> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(items => items is null || items.Count <= SortOrderLimits.MaxItems).WithMessage(message);
    }

    /// <summary>
    /// Requires a translation map to hold an entry for the panel's default locale, the one every other
    /// locale falls back to when a reader's own language has no text.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <typeparam name="TTranslation">The translation DTO the map holds.</typeparam>
    /// <param name="rule">The rule builder for the translations property.</param>
    /// <param name="message">The localized sentence reported when the default locale is missing.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, Dictionary<string, TTranslation>> MustIncludeDefaultLocale<T, TTranslation>(
        this IRuleBuilder<T, Dictionary<string, TTranslation>> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(map => map is not null && map.ContainsKey(SupportedLanguages.Default)).WithMessage(message);
    }

    /// <summary>
    /// Requires a translation list to hold an entry for the panel's default locale; see
    /// <see cref="MustIncludeDefaultLocale{T, TTranslation}(IRuleBuilder{T, Dictionary{string, TTranslation}}, string)"/>.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <typeparam name="TTranslation">The translation DTO the list holds.</typeparam>
    /// <param name="rule">The rule builder for the translations property.</param>
    /// <param name="localeOf">Reads the locale code of a translation.</param>
    /// <param name="message">The localized sentence reported when the default locale is missing.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, List<TTranslation>> MustIncludeDefaultLocale<T, TTranslation>(
        this IRuleBuilder<T, List<TTranslation>> rule, Func<TTranslation, string> localeOf, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localeOf);

        return rule.Must(list => list is not null && list.Any(t => t is not null && localeOf(t) == SupportedLanguages.Default))
            .WithMessage(message);
    }

    /// <summary>Requires a translation list to name each locale at most once.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <typeparam name="TTranslation">The translation DTO the list holds.</typeparam>
    /// <param name="rule">The rule builder for the translations property.</param>
    /// <param name="localeOf">Reads the locale code of a translation.</param>
    /// <param name="message">The localized sentence reported when a locale repeats.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, List<TTranslation>> MustHaveDistinctLocales<T, TTranslation>(
        this IRuleBuilder<T, List<TTranslation>> rule, Func<TTranslation, string> localeOf, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localeOf);

        return rule.Must(list => list is null
                || list.Where(t => t is not null).Select(localeOf).Distinct(StringComparer.Ordinal).Count()
                    == list.Count(t => t is not null))
            .WithMessage(message);
    }


    /// <summary>Whether a value is an absolute http(s) address.</summary>
    /// <param name="value">The address to inspect.</param>
    /// <returns><see langword="true"/> when the address is absolute and uses http or https.</returns>
    private static bool IsAbsoluteWebUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Whether a value is empty, a root-relative path, or an absolute http(s) address.</summary>
    /// <param name="value">The link to inspect.</param>
    /// <returns><see langword="true"/> when the link may be rendered.</returns>
    private static bool IsWebLink(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (value[0] == '/')
        {
            return !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\');
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
