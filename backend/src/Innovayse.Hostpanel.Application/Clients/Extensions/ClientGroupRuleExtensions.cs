using FluentValidation;
using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// Rule chains for the fields a client group is created and updated with, named once so the two
/// validators cannot drift apart. The limits come from <see cref="ClientGroup"/>.
/// </summary>
public static class ClientGroupRuleExtensions
{
    /// <summary>Requires a group name: not blank, within the stored column.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the name property.</param>
    /// <param name="message">The localized sentence reported when the name is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeClientGroupName<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.NotEmpty().MaximumLength(ClientGroup.NameMaxLength).WithMessage(message);
    }

    /// <summary>Requires a group description, when one is given, to fit the stored column.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the description property.</param>
    /// <param name="message">The localized sentence reported when the description is too long.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeClientGroupDescription<T>(this IRuleBuilder<T, string?> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.MaximumLength(ClientGroup.DescriptionMaxLength).WithMessage(message);
    }

    /// <summary>Requires a discount between 0 and 100 percent, with at most the decimals the stored column keeps.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the discount property.</param>
    /// <param name="rangeMessage">The localized sentence reported when the discount is out of range.</param>
    /// <param name="decimalsMessage">The localized sentence reported when the discount has more decimals than the column keeps.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> MustBeDiscountPercent<T>(
        this IRuleBuilder<T, decimal> rule, string rangeMessage, string decimalsMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.InclusiveBetween(0m, ClientGroup.MaxDiscountPercent).WithMessage(rangeMessage)
            .PrecisionScale(ClientGroup.DiscountPercentPrecision, ClientGroup.DiscountPercentScale, ignoreTrailingZeros: true)
            .WithMessage(decimalsMessage);
    }

    /// <summary>Requires a colour written as <c>#RRGGBB</c>, by the same check the domain applies.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the colour property.</param>
    /// <param name="message">The localized sentence reported when the colour is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeClientGroupColor<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(ClientGroup.IsValidColor).WithMessage(message);
    }
}
