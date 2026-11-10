using Innovayse.Hostpanel.Domain.Orders.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="DomainAction"/> in <c>OrderItems.DomainAction</c> as the member name.
/// </summary>
/// <remarks>
/// Rows written before the action was stored by name held the lower-case texts "register", "transfer"
/// and "renew"; the <c>StoreOrderItemDomainActionByName</c> migration rewrites them to the member name
/// and stops on any other text. After it only member names are in the column, so a text that is not
/// exactly a member name is a data fault and fails the read loudly instead of guessing an action.
/// </remarks>
public sealed class DomainActionConverter : ValueConverter<DomainAction, string>
{
    /// <summary>Initialises the converter.</summary>
    public DomainActionConverter()
        : base(action => ToStored(action), value => FromStored(value))
    {
    }

    /// <summary>Gets the stored form of an action.</summary>
    /// <param name="action">The domain action to store.</param>
    /// <returns>The column value: the member name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined action.</exception>
    public static string ToStored(DomainAction action) => action switch
    {
        DomainAction.Register => nameof(DomainAction.Register),
        DomainAction.Transfer => nameof(DomainAction.Transfer),
        DomainAction.Renew => nameof(DomainAction.Renew),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown domain action."),
    };

    /// <summary>Reads a stored value back.</summary>
    /// <param name="value">The column value.</param>
    /// <returns>The action the text names.</returns>
    /// <exception cref="InvalidOperationException">The text is not a member name.</exception>
    public static DomainAction FromStored(string value) => value switch
    {
        nameof(DomainAction.Register) => DomainAction.Register,
        nameof(DomainAction.Transfer) => DomainAction.Transfer,
        nameof(DomainAction.Renew) => DomainAction.Renew,
        _ => throw new InvalidOperationException($"'{value}' is not a domain action."),
    };
}
