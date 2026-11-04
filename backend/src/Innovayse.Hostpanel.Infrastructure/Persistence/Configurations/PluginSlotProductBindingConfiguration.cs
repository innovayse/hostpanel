using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="PluginSlotProductBinding"/>.</summary>
public sealed class PluginSlotProductBindingConfiguration : IEntityTypeConfiguration<PluginSlotProductBinding>
{
    /// <summary>
    /// The unique index on plugin, slot, widget and product. Named so the context can tell two first saves of the same
    /// binding (a violation of this index) from any other failure.
    /// </summary>
    public const string BindingIndex = "IX_PluginSlotProductBindings_PluginId_SlotId_WidgetId_ProductId";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PluginSlotProductBinding> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PluginId).IsRequired().HasMaxLength(PluginIdentifier.MaxLength);
        builder.Property(x => x.SlotId).IsRequired().HasMaxLength(PluginSlotProductBinding.IdMaxLength);
        builder.Property(x => x.WidgetId).IsRequired().HasMaxLength(PluginSlotProductBinding.IdMaxLength);
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.PluginId, x.SlotId, x.WidgetId, x.ProductId })
            .IsUnique()
            .HasDatabaseName(BindingIndex);
    }
}
