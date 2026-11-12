using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="TaxRule"/>.</summary>
public sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(TaxRule.NameMaxLength);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(TaxRule.CountryLength);
        builder.Property(x => x.Region).HasMaxLength(TaxRule.RegionMaxLength);
        builder.Property(x => x.RatePercent).IsRequired().HasColumnType("numeric(6,3)");
        builder.Property(x => x.IsEnabled).IsRequired();

        // One rule per place for every writer. Nulls count as equal, so a second country-wide rule is refused too.
        builder.HasIndex(x => new { x.Country, x.Region }).IsUnique().AreNullsDistinct(false);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TaxRules_RatePercent",
            "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100"));
    }
}
