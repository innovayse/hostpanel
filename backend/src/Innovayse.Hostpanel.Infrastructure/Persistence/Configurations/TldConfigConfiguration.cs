using System.Text.Json;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="TldConfig"/> entity.</summary>
public sealed class TldConfigConfiguration : IEntityTypeConfiguration<TldConfig>
{
    /// <summary>
    /// Compares two price tables (years to price) by content, not by reference. The column is one
    /// jsonb value converted from the dictionary, so without it EF would compare references and
    /// miss a price changed in place, or write the column again for an unchanged table.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyDictionary<int, decimal>> PriceTableComparer = new(
        (a, b) => a == null
            ? b == null
            : b != null && a.Count == b.Count && a.OrderBy(entry => entry.Key).SequenceEqual(b.OrderBy(entry => entry.Key)),
        v => v.Aggregate(0, (hash, entry) => hash ^ HashCode.Combine(entry.Key, entry.Value)),
        v => new Dictionary<int, decimal>(v));

    /// <summary>
    /// Compares two category lists by content and order, for the same reason as
    /// <see cref="PriceTableComparer"/>: the list is stored as one jsonb value.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<string>> CategoriesComparer = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        v => v.ToList());

    /// <summary>Configures the <c>TldConfigs</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TldConfig> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tld).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RegistrarModule).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.SellCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.LastSyncedAt);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Tld).IsUnique();
        builder.HasIndex(x => x.IsEnabled);

        // jsonb price dictionaries: use backing fields so EF writes to Dictionary<int, decimal> directly.
        MapPriceTable(builder.Property(x => x.CostRegister), "_costRegister");
        MapPriceTable(builder.Property(x => x.CostTransfer), "_costTransfer");
        MapPriceTable(builder.Property(x => x.CostRenew), "_costRenew");
        MapPriceTable(builder.Property(x => x.SellRegister), "_sellRegister");
        MapPriceTable(builder.Property(x => x.SellTransfer), "_sellTransfer");
        MapPriceTable(builder.Property(x => x.SellRenew), "_sellRenew");

        // jsonb categories: use backing field.
        builder.Property(x => x.Categories)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)
                     ?? new List<string>(),
                CategoriesComparer)
            .HasField("_categories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    /// <summary>
    /// Maps one price table as a jsonb column written through its backing field, with the
    /// content comparer, so the six tables share one mapping instead of six copies of it.
    /// </summary>
    /// <param name="property">The price table property.</param>
    /// <param name="fieldName">The backing field EF reads and writes.</param>
    private static void MapPriceTable(PropertyBuilder<IReadOnlyDictionary<int, decimal>> property, string fieldName)
    {
        property
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>(),
                PriceTableComparer)
            .HasField(fieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
