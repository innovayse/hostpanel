using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Domain.Billing.Currency"/>.</summary>
public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Domain.Billing.Currency>
{
    /// <summary>Column width for a three-character ISO code.</summary>
    private const int IsoCodeLength = 3;

    /// <summary>Column width for a display prefix or suffix.</summary>
    private const int AffixLength = 10;

    /// <summary>
    /// Predicate of the partial unique index over <see cref="Domain.Billing.Currency.IsBase"/>. The column name
    /// is EF's default: the property name, quoted because it is mixed-case in PostgreSQL.
    /// </summary>
    private const string BaseRowFilter = "\"IsBase\" = true";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Domain.Billing.Currency> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(IsoCodeLength);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Numeric).IsRequired().HasMaxLength(IsoCodeLength);
        builder.Property(x => x.Prefix).IsRequired().HasMaxLength(AffixLength);
        builder.Property(x => x.Suffix).IsRequired().HasMaxLength(AffixLength);
        builder.Property(x => x.Decimals).IsRequired();
        // Eight decimal places keeps 1/390 (0.00256410) exact enough that a round trip through
        // "update product prices" does not drift a whole dram.
        builder.Property(x => x.RateToBase).HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.IsBase).IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();
        // Only one base: a partial unique index over the true rows.
        builder.HasIndex(x => x.IsBase).IsUnique().HasFilter(BaseRowFilter);
        builder.Ignore(x => x.DomainEvents);
    }
}
