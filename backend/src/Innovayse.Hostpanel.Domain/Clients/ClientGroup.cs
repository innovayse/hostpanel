using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients;

/// <summary>
/// A named group staff put clients into (for example "Wholesale"), carrying the discount its members
/// are due and the colour the admin panel shows it in. At most one group is the default: new clients
/// join it when they are created without a group of their own.
/// Stored in the <c>ClientGroups</c> table.
/// </summary>
public sealed class ClientGroup : BaseEntity, IAggregateRoot
{
    /// <summary>Longest group name the stored column holds.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Longest description the stored column holds.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Length of a colour written as <c>#RRGGBB</c>; the <c>ClientGroups.Color</c> column is this wide.</summary>
    public const int ColorLength = 7;

    /// <summary>The largest discount a group can give, in percent.</summary>
    public const decimal MaxDiscountPercent = 100m;

    /// <summary>Digits the <c>ClientGroups.DiscountPercent</c> column holds in all, before and after the decimal point.</summary>
    public const int DiscountPercentPrecision = 5;

    /// <summary>Decimals the <c>ClientGroups.DiscountPercent</c> column keeps; a finer discount would be rounded on save.</summary>
    public const int DiscountPercentScale = 2;

    /// <summary>The character a colour starts with.</summary>
    private const char ColorPrefix = '#';

    /// <summary>Gets the display name of the group.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the free-text description, or <see langword="null"/> when there is none.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the discount members are due, in percent, from 0 to <see cref="MaxDiscountPercent"/>.</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>Gets the colour the admin panel shows the group in, as upper-case <c>#RRGGBB</c>.</summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>Gets whether new clients created without a group join this one.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ClientGroup() { }

    /// <summary>Creates a group that is not the default.</summary>
    /// <param name="name">The display name; not blank, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="description">The optional description, at most <see cref="DescriptionMaxLength"/> characters.</param>
    /// <param name="discountPercent">The discount in percent, from 0 to <see cref="MaxDiscountPercent"/>.</param>
    /// <param name="color">The colour as <c>#RRGGBB</c>.</param>
    /// <returns>A new, unpersisted <see cref="ClientGroup"/>.</returns>
    /// <exception cref="ArgumentException">The name is blank or a text is longer than its column.</exception>
    /// <exception cref="DomainRuleException">The discount is out of range or the colour is not <c>#RRGGBB</c>.</exception>
    public static ClientGroup Create(string name, string? description, decimal discountPercent, string color)
    {
        var group = new ClientGroup();
        group.Update(name, description, discountPercent, color);
        return group;
    }

    /// <summary>Replaces the group's name, description, discount and colour.</summary>
    /// <param name="name">The display name; not blank, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="description">The optional description, at most <see cref="DescriptionMaxLength"/> characters.</param>
    /// <param name="discountPercent">The discount in percent, from 0 to <see cref="MaxDiscountPercent"/>.</param>
    /// <param name="color">The colour as <c>#RRGGBB</c>.</param>
    /// <exception cref="ArgumentException">The name is blank or a text is longer than its column.</exception>
    /// <exception cref="DomainRuleException">The discount is out of range or the colour is not <c>#RRGGBB</c>.</exception>
    public void Update(string name, string? description, decimal discountPercent, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"A client group's name is at most {NameMaxLength} characters.", nameof(name));
        }

        if (description is { Length: > DescriptionMaxLength })
        {
            throw new ArgumentException($"A client group's description is at most {DescriptionMaxLength} characters.", nameof(description));
        }

        if (discountPercent is < 0m or > MaxDiscountPercent)
        {
            throw new DomainRuleException(ClientsErrors.ClientGroupDiscountInvalid, "A discount is between 0 and 100 percent.");
        }

        if (!IsValidColor(color))
        {
            throw new DomainRuleException(ClientsErrors.ClientGroupColorInvalid, "A colour is written as #RRGGBB.");
        }

        Name = name;
        Description = description;
        DiscountPercent = discountPercent;
        Color = color.ToUpperInvariant();
    }

    /// <summary>
    /// Takes a group's discount off a list price, rounded to the currency's minor unit. The one place the
    /// discount of a client group becomes an amount, so an order line and its invoice line always agree.
    /// </summary>
    /// <param name="listPrice">The price before the discount, in <paramref name="currency"/>.</param>
    /// <param name="discountPercent">The discount in percent, from 0 to <see cref="MaxDiscountPercent"/>.</param>
    /// <param name="currency">ISO 4217 alpha code the price is in.</param>
    /// <returns>The price after the discount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The discount is outside 0 to <see cref="MaxDiscountPercent"/>.</exception>
    public static decimal PriceAfterDiscount(decimal listPrice, decimal discountPercent, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(discountPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(discountPercent, MaxDiscountPercent);

        return CurrencyCode.RoundToMinorUnits(listPrice * (1m - (discountPercent / MaxDiscountPercent)), currency);
    }

    /// <summary>
    /// Makes this group the only default: the flag is cleared on every other group in <paramref name="others"/>,
    /// so at most one group is the default.
    /// </summary>
    /// <param name="others">The groups that may hold the flag now; this group, if present, is left alone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is null.</exception>
    public void MakeDefault(IEnumerable<ClientGroup> others)
    {
        ArgumentNullException.ThrowIfNull(others);

        foreach (var other in others)
        {
            if (!ReferenceEquals(other, this))
            {
                other.ClearDefault();
            }
        }

        IsDefault = true;
    }

    /// <summary>Stops this group being the default. Does nothing when it is not.</summary>
    public void ClearDefault()
    {
        IsDefault = false;
    }

    /// <summary>Whether the text is a colour written as <c>#RRGGBB</c>.</summary>
    /// <param name="color">The text to check.</param>
    /// <returns><see langword="true"/> when it is a hash followed by six hexadecimal digits.</returns>
    public static bool IsValidColor(string? color)
    {
        return color is { Length: ColorLength }
            && color[0] == ColorPrefix
            && color.Skip(1).All(Uri.IsHexDigit);
    }
}
