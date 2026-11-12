namespace Innovayse.Hostpanel.Domain.Products.Enums;

/// <summary>How a client picks a value for a configurable option group when ordering a product.</summary>
public enum ProductOptionType
{
    /// <summary>The client picks one of several options from a list.</summary>
    Dropdown = 0,

    /// <summary>The client picks one of several options shown side by side.</summary>
    Radio = 1,

    /// <summary>The client switches a single option on or off.</summary>
    Checkbox = 2,

    /// <summary>The client enters an amount between the group's minimum and maximum.</summary>
    Quantity = 3,

    /// <summary>The client types a free text value; the option carries no price.</summary>
    Text = 4,
}
