namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A single line item on an <see cref="Invoice"/>.
/// Owned by the Invoice aggregate; stored in the <c>invoice_items</c> table.
/// </summary>
public sealed class InvoiceItem : Entity
{
    /// <summary>Gets the FK to the parent <see cref="Invoice"/> (set by EF after save).</summary>
    public int InvoiceId { get; private set; }

    /// <summary>
    /// Gets the FK to the <c>ClientService</c> this line was charged for, or
    /// <see langword="null"/> when the line is not a charge against one service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nullable, and the null means two different things that must not be conflated. A line may
    /// genuinely not be about a service -- a one-off billable item, a domain registration, an
    /// admin-entered adjustment -- and a line may be about a service that nobody recorded,
    /// because every row written before this column existed has no link and none can honestly be
    /// invented for it. The invoice item carries a description, a unit price and a quantity;
    /// matching that description text against a product name would be a guess rendered as fact,
    /// and this platform bills real money.
    /// </para>
    /// <para>
    /// Callers reading "the invoices for a service" therefore have to distinguish "nothing was
    /// charged" from "nothing was recorded". <c>GetMyServiceInvoicesQuery</c> answers both
    /// separately for exactly this reason.
    /// </para>
    /// <para>
    /// Known gap: <c>UpdateInvoiceItemsHandler</c> replaces an invoice's lines wholesale rather
    /// than editing them, so an admin who edits the lines of a linked invoice drops the links.
    /// That path predates this column and is left alone here rather than reworked as a side
    /// effect; the result is a lost link, never a wrong one.
    /// </para>
    /// </remarks>
    public int? ClientServiceId { get; private set; }

    /// <summary>Gets the human-readable description of the charge.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the number of units.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the line total (<see cref="UnitPrice"/> × <see cref="Quantity"/>).</summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private InvoiceItem() : base(0) { }

    /// <summary>
    /// Creates a new invoice line item.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <param name="clientServiceId">
    /// FK to the service this line is charged for, or <see langword="null"/> when the line is
    /// not a charge against one service or the caller does not know which. Optional so the
    /// eleven existing call sites that have no service in hand keep compiling and keep meaning
    /// what they meant; only a caller holding the actual <c>ClientService</c> passes it.
    /// </param>
    /// <returns>A new <see cref="InvoiceItem"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative or <paramref name="quantity"/> is less than 1.</exception>
    public static InvoiceItem Create(
        string description, decimal unitPrice, int quantity, int? clientServiceId = null)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be >= 0.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1.");
        }

        return new()
        {
            Description = description,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Amount = unitPrice * quantity,
            ClientServiceId = clientServiceId,
        };
    }

    /// <summary>
    /// Updates the line item properties and recalculates <see cref="Amount"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately leaves <see cref="ClientServiceId"/> alone. Re-wording a description or
    /// correcting a price does not change which service the money was for, and an edit path that
    /// silently cleared the link would make a charge stop showing on the service it belongs to.
    /// </remarks>
    /// <param name="description">New human-readable charge description.</param>
    /// <param name="unitPrice">New price per unit (≥ 0).</param>
    /// <param name="quantity">New number of units (≥ 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative or <paramref name="quantity"/> is less than 1.</exception>
    public void Update(string description, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be >= 0.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1.");
        }

        Description = description;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Amount = unitPrice * quantity;
    }
}
