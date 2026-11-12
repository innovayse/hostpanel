using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// Rules shared by <see cref="ProductOptionGroup"/> and <see cref="ProductOption"/>, so a name and
/// a price are judged the same way wherever they are written.
/// </summary>
internal static class ProductOptionRules
{
    /// <summary>The largest percentage a <see cref="ProductOptionPriceType.Percentage"/> modifier may be.</summary>
    internal const decimal MaxPercentage = 100m;

    /// <summary>Trims a name and refuses a blank or over-long one.</summary>
    /// <param name="name">The name as given.</param>
    /// <param name="maxLength">The longest name the column holds.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="ArgumentException">The name is blank or longer than <paramref name="maxLength"/>.</exception>
    internal static string NormaliseName(string name, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"A name cannot be longer than {maxLength} characters.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>Refuses a negative amount, and a percentage above <see cref="MaxPercentage"/>.</summary>
    /// <param name="amount">The modifier or setup fee modifier.</param>
    /// <param name="priceType">How the amount is read.</param>
    /// <exception cref="DomainRuleException">The amount is negative, or a percentage above 100.</exception>
    internal static void EnsurePrice(decimal amount, ProductOptionPriceType priceType)
    {
        if (amount < 0m || (priceType == ProductOptionPriceType.Percentage && amount > MaxPercentage))
        {
            throw new DomainRuleException(
                ProductsErrors.ProductOptionPriceOutOfRange,
                $"A {priceType} modifier of {amount} is outside its range.");
        }
    }
}
