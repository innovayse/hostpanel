using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// Records that one order line extended a domain at the registrar. The set of these is what makes renewing a domain for an
/// order line idempotent: a retry of the line finds its own row and does not extend the domain a second time, and a renewal
/// made some other way (staff, the renewal job, another line) adds no row and erases none, so it can never make the
/// line forget what it already bought. Stored in the <c>DomainLineRenewals</c> table.
/// </summary>
public sealed class DomainLineRenewal : BaseEntity
{
    /// <summary>Gets the FK to the renewed domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>
    /// Gets the id of the order line whose renewal extended the domain. At most one row exists per line. Index only, no
    /// FK: the order line belongs to Orders.
    /// </summary>
    public int OrderItemId { get; private set; }

    /// <summary>Gets the UTC moment the renewal was made.</summary>
    public DateTimeOffset RenewedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private DomainLineRenewal() { }

    /// <summary>Creates the record of a renewal made for an order line.</summary>
    /// <param name="domainId">FK to the renewed domain.</param>
    /// <param name="orderItemId">The order line the renewal was bought on.</param>
    /// <param name="renewedAt">The moment the renewal was made, UTC.</param>
    internal DomainLineRenewal(int domainId, int orderItemId, DateTimeOffset renewedAt)
    {
        DomainId = domainId;
        OrderItemId = orderItemId;
        RenewedAt = renewedAt;
    }
}
