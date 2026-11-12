using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Application.Orders.Extensions;

/// <summary>Extension methods the order flow uses to put a <see cref="PricedOrderLine"/> into words for an invoice.</summary>
public static class PricedOrderLineExtensions
{
    /// <summary>
    /// The invoice line text for an order line: the product, for a domain line the domain it is for, and for a line of
    /// a bundle order the bundle it was sold in.
    /// </summary>
    /// <remarks>
    /// The text never outgrows <see cref="InvoiceItem.DescriptionMaxLength"/>, which the invoice refuses. When the
    /// product, the domain and the bundle together would, the product and domain part is cut and the bundle's name
    /// is kept whole: a bundle's name is capped at <see cref="ProductBundle.NameMaxLength"/>, so it always fits.
    /// </remarks>
    /// <param name="line">The priced line.</param>
    /// <param name="bundleName">The name of the bundle the order is placed from, or <see langword="null"/> for an ordinary order.</param>
    /// <returns>The description the invoice carries.</returns>
    public static string Describe(this PricedOrderLine line, string? bundleName)
    {
        ArgumentNullException.ThrowIfNull(line);

        var description = line.Item.DomainAction is not null && !string.IsNullOrWhiteSpace(line.Item.Domain)
            ? $"{line.ProductName} ({line.Item.Domain})"
            : line.ProductName;
        var suffix = bundleName is null ? string.Empty : $" ({bundleName})";

        var room = InvoiceItem.DescriptionMaxLength - suffix.Length;
        return (description.Length > room ? description[..room] : description) + suffix;
    }
}
