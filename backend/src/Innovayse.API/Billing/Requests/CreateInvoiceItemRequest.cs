namespace Innovayse.API.Billing.Requests;

/// <summary>A single line item submitted in <see cref="CreateInvoiceRequest"/>.</summary>
public sealed class CreateInvoiceItemRequest
{
    /// <summary>Gets or initialises the human-readable charge description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }
}
