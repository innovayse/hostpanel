using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="VatRate"/>.</summary>
public sealed class VatRateConfiguration : IEntityTypeConfiguration<VatRate>
{
    /// <summary>Widest the stored name of a <see cref="Domain.Billing.Enums.VatRateKind"/> can be.</summary>
    private const int KindLength = 20;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VatRate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Country).IsRequired().HasMaxLength(VatRate.CountryLength);
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(KindLength);
        builder.Property(x => x.RatePercent).IsRequired().HasColumnType("numeric(6,3)");
        builder.Property(x => x.ValidFrom).IsRequired();

        // One row per country, kind and day for every writer, the startup import included.
        builder.HasIndex(x => new { x.Country, x.Kind, x.ValidFrom }).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_VatRates_RatePercent",
            "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100"));
    }
}
