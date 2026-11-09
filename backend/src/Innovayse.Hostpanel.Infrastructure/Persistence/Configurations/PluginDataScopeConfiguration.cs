using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="PluginDataScope"/>.</summary>
public sealed class PluginDataScopeConfiguration : IEntityTypeConfiguration<PluginDataScope>
{
    /// <summary>
    /// The unique index on plugin and client reference. Named so the context can tell a lost race for the first write of
    /// one scope (two calls opening the same scope row) from any other failure.
    /// </summary>
    public const string ScopeIndex = "IX_PluginDataScopes_PluginId_ClientRef";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginDataScope> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PluginId).IsRequired().HasMaxLength(PluginIdentifier.MaxLength);
        builder.Property(x => x.ClientRef).HasMaxLength(PluginDataEntry.ClientRefLength);
        builder.Property(x => x.KeyCount).IsRequired();
        builder.Property(x => x.StoredChars).IsRequired();
        builder.Property(x => x.Revision).IsRequired();

        // Every write of a scope moves Revision, so every write updates this row; xmin then makes two writes of one
        // scope that ran at the same time conflict on save, which is what keeps the totals and the quota exact.
        builder.UseXminConcurrencyToken();

        // One row per plugin and client reference. Nulls are not distinct, so the plugin-wide scope (no client
        // reference) is one row too.
        builder.HasIndex(x => new { x.PluginId, x.ClientRef })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName(ScopeIndex);
    }
}
