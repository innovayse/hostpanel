using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="PluginDataEntry"/>.</summary>
public sealed class PluginDataEntryConfiguration : IEntityTypeConfiguration<PluginDataEntry>
{
    /// <summary>
    /// The unique index on plugin, client reference and key. Named so the store can tell a lost race for the first write
    /// of one key (a violation of this index) from any other failure.
    /// </summary>
    public const string ScopeKeyIndex = "IX_PluginDataEntries_PluginId_ClientRef_Key";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginDataEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PluginId).IsRequired().HasMaxLength(PluginIdentifier.MaxLength);
        builder.Property(x => x.ClientRef).HasMaxLength(PluginDataEntry.ClientRefLength);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(PluginDataEntry.MaxKeyLength);

        // The SDK store keeps text, not JSON, so the column is plain text; the entity caps its length
        // (PluginDataEntry.MaxValueLength).
        builder.Property(x => x.Value).IsRequired().HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();

        // One row per plugin, client reference and key. Nulls are not distinct, so a plugin-wide value (no client
        // reference) is one row per key too.
        builder.HasIndex(x => new { x.PluginId, x.ClientRef, x.Key })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName(ScopeKeyIndex);
    }
}
