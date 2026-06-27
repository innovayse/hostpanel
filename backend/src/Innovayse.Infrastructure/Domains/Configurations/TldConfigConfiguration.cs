namespace Innovayse.Infrastructure.Domains.Configurations;

using System.Text.Json;
using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="TldConfig"/> entity.</summary>
public sealed class TldConfigConfiguration : IEntityTypeConfiguration<TldConfig>
{
    /// <summary>Configures the <c>tld_configs</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TldConfig> builder)
    {
        builder.ToTable("tld_configs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tld).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RegistrarModule).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.SellCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.LastSyncedAt);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Tld).IsUnique();
        builder.HasIndex(x => x.IsEnabled);

        // jsonb price dictionaries — use backing fields so EF writes to Dictionary<int, decimal> directly.
        builder.Property(x => x.CostRegister)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_costRegister")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.CostTransfer)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_costTransfer")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.CostRenew)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_costRenew")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.SellRegister)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_sellRegister")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.SellTransfer)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_sellTransfer")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.SellRenew)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<int, decimal>())
            .HasField("_sellRenew")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // jsonb categories — use backing field.
        builder.Property(x => x.Categories)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)
                     ?? new List<string>())
            .HasField("_categories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
