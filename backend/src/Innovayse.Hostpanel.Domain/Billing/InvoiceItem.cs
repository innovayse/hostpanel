using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A single line item on an <see cref="Invoice"/>.
/// Owned by the Invoice aggregate; stored in the <c>InvoiceItems</c> table.
/// </summary>
public sealed class InvoiceItem : BaseEntity
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
    /// genuinely not be about a service, a one-off billable item, a domain registration, an
    /// admin-entered adjustment, and a line may be about a service that nobody recorded,
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
    /// <c>UpdateInvoiceItemsHandler</c> edits an existing line in place (<c>Invoice.UpdateItem</c>
    /// changes description, price and quantity only), so an admin edit keeps this link and the
    /// renewal columns; a line deleted there takes its link with it, and a line added there carries
    /// none.
    /// </para>
    /// </remarks>
    public int? ClientServiceId { get; private set; }

    /// <summary>
    /// Gets the id of the registered domain this line charges for, or <see langword="null"/>.
    /// Index only, no FK, for the same reason as <see cref="ClientServiceId"/>: the invoice is a
    /// financial record that must outlive the domain row.
    /// </summary>
    public int? RegisteredDomainId { get; private set; }

    /// <summary>Gets the start of the period this renewal line pays for, or <see langword="null"/> when it is not a renewal.</summary>
    public DateTimeOffset? RenewalPeriodStart { get; private set; }

    /// <summary>Gets the end of the period this renewal line pays for, or <see langword="null"/> when it is not a renewal.</summary>
    public DateTimeOffset? RenewalPeriodEnd { get; private set; }

    /// <summary>
    /// Gets when the paid period was applied to its service or domain, or <see langword="null"/>
    /// while it is not. This marker, not the payment event, is what makes a renewal happen exactly once.
    /// </summary>
    public DateTimeOffset? RenewalAppliedAt { get; private set; }

    /// <summary>
    /// Gets when a runner claimed this line before an outward registrar call, or
    /// <see langword="null"/> while nobody has. Set by <see cref="ClaimRenewal"/>; this and
    /// <see cref="RenewalAppliedAt"/> are optimistic concurrency tokens, so the save of a claim only
    /// wins while both still hold the values the runner read and two runners can never both renew
    /// the same paid period at the registrar. Cleared again only by
    /// <see cref="ReleaseRenewalClaim"/> when the registrar cleanly refused; after an unknown outcome
    /// it stays set and the line waits for a person.
    /// </summary>
    public DateTimeOffset? RenewalAttemptAt { get; private set; }

    /// <summary>
    /// Gets what the product this line charges for was at billing time, or <see langword="null"/> when the
    /// line is not a product's charge (a domain renewal, a billable item, an adjustment) or was written
    /// before snapshots were kept. Set once when the line is created and never changed after: an edit of the
    /// description or price and a later change in the catalogue both leave it as it was sold.
    /// </summary>
    public ProductSnapshot? ProductSnapshot { get; private set; }

    /// <summary>Gets whether this line charges for a digital product, as it was when billed.</summary>
    public bool IsDigital => ProductSnapshot is { IsDigital: true };

    /// <summary>Gets whether this line is a renewal (it carries a period). Lines written before renewals were tracked never are.</summary>
    public bool IsRenewal => RenewalPeriodStart is not null;

    /// <summary>Gets the human-readable description of the charge.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the price per unit.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets the number of units.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the line total (<see cref="UnitPrice"/> × <see cref="Quantity"/>).</summary>
    public decimal Amount { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private InvoiceItem() { }

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
    /// <param name="productSnapshot">
    /// What the product charged for was at billing time, or <see langword="null"/> when the line is not a
    /// product's charge. Optional for the same reason as <paramref name="clientServiceId"/>: only a caller
    /// that read the product passes it, and nothing may reconstruct it from the description.
    /// </param>
    /// <returns>A new <see cref="InvoiceItem"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unitPrice"/> is negative or <paramref name="quantity"/> is less than 1.</exception>
    public static InvoiceItem Create(
        string description, decimal unitPrice, int quantity, int? clientServiceId = null, ProductSnapshot? productSnapshot = null)
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
            ProductSnapshot = productSnapshot,
        };
    }

    /// <summary>Creates a renewal line for exactly one service or one domain over one period.</summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="amount">Price of the period (≥ 0).</param>
    /// <param name="clientServiceId">The service renewed, or <see langword="null"/> for a domain line.</param>
    /// <param name="registeredDomainId">The domain renewed, or <see langword="null"/> for a service line.</param>
    /// <param name="start">Period start.</param>
    /// <param name="end">Period end; must be after <paramref name="start"/>.</param>
    /// <param name="productSnapshot">What the renewed service's product is at billing time, or <see langword="null"/> for a domain line.</param>
    /// <returns>The new line.</returns>
    /// <exception cref="ArgumentException">Not exactly one target, an empty period, or a product snapshot on a domain line.</exception>
    internal static InvoiceItem CreateRenewal(
        string description, decimal amount, int? clientServiceId, int? registeredDomainId,
        DateTimeOffset start, DateTimeOffset end, ProductSnapshot? productSnapshot)
    {
        if ((clientServiceId is null) == (registeredDomainId is null))
        {
            throw new ArgumentException("A renewal line renews exactly one service or one domain.");
        }

        if (end <= start)
        {
            throw new ArgumentException("A renewal period must end after it starts.", nameof(end));
        }

        // A domain is not a catalogue product, so a domain line can never carry a product's snapshot.
        if (registeredDomainId is not null && productSnapshot is not null)
        {
            throw new ArgumentException("A domain renewal line carries no product snapshot.", nameof(productSnapshot));
        }

        var item = Create(description, amount, 1, clientServiceId, productSnapshot);
        item.RegisteredDomainId = registeredDomainId;
        item.RenewalPeriodStart = start;
        item.RenewalPeriodEnd = end;
        return item;
    }

    /// <summary>Records that the paid period has been applied to its service or domain.</summary>
    /// <param name="at">When it was applied (UTC).</param>
    /// <exception cref="InvalidOperationException">The line is not a renewal, or was already applied.</exception>
    public void MarkRenewalApplied(DateTimeOffset at)
    {
        if (!IsRenewal)
        {
            throw new InvalidOperationException("Only a renewal line can be applied.");
        }

        if (RenewalAppliedAt is not null)
        {
            throw new InvalidOperationException("This renewal line was already applied.");
        }

        RenewalAppliedAt = at;
    }

    /// <summary>Claims this renewal line for one runner before an outward registrar call.</summary>
    /// <param name="at">When the claim was taken (UTC).</param>
    /// <exception cref="InvalidOperationException">The line is not a renewal, or is already applied or claimed.</exception>
    public void ClaimRenewal(DateTimeOffset at)
    {
        if (!IsRenewal)
        {
            throw new InvalidOperationException("Only a renewal line can be claimed.");
        }

        if (RenewalAppliedAt is not null || RenewalAttemptAt is not null)
        {
            throw new InvalidOperationException("This renewal line is already applied or claimed.");
        }

        RenewalAttemptAt = at;
    }

    /// <summary>Releases this runner's claim after the registrar cleanly refused, so a later run may retry.</summary>
    /// <exception cref="InvalidOperationException">The line is not claimed.</exception>
    public void ReleaseRenewalClaim()
    {
        if (RenewalAttemptAt is null)
        {
            throw new InvalidOperationException("This renewal line is not claimed.");
        }

        RenewalAttemptAt = null;
    }

    /// <summary>
    /// Updates the line item properties and recalculates <see cref="Amount"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately leaves <see cref="ClientServiceId"/> and <see cref="ProductSnapshot"/> alone.
    /// Re-wording a description or correcting a price does not change which service the money was
    /// for or what was sold, and an edit path that silently cleared either would make a charge stop
    /// showing on the service it belongs to, or change what a fulfilment plugin delivers for it.
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
