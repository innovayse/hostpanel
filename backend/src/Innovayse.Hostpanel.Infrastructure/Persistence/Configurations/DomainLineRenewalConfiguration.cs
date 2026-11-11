using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="DomainLineRenewal"/> entity.
/// Maps to the <c>DomainLineRenewals</c> table.
/// </summary>
internal sealed class DomainLineRenewalConfiguration : IEntityTypeConfiguration<DomainLineRenewal>
{
    /// <summary>
    /// The name of the unique index on the order line a renewal was made for, the default EF already gave it, named here
    /// so <see cref="AppDbContext"/> can tell its violation from any other.
    /// </summary>
    public const string OrderItemIndex = "IX_DomainLineRenewals_OrderItemId";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DomainLineRenewal> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DomainId).IsRequired();
        builder.Property(r => r.OrderItemId).IsRequired();
        builder.Property(r => r.RenewedAt).IsRequired();

        // An order line renews at most one domain, once: the database refuses a second row for the line, and the
        // index also serves the lookup "did this line already renew".
        builder.HasIndex(r => r.OrderItemId).IsUnique().HasDatabaseName(OrderItemIndex);

        // The per-domain load of the set the renewal use case reads.
        builder.HasIndex(r => r.DomainId);
    }
}
