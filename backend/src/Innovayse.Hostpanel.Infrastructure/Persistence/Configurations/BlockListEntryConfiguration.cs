using Innovayse.Hostpanel.Domain.Fraud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="BlockListEntry"/>.</summary>
public sealed class BlockListEntryConfiguration : IEntityTypeConfiguration<BlockListEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlockListEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(BlockListValue.MaxLength);
        builder.Property(x => x.Note).HasMaxLength(BlockListEntry.NoteMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        // One value is listed once per type; the add use case answers a repeat with a code, the index is the backstop for two
        // adds racing, and it serves the list filtered by type.
        builder.HasIndex(x => new { x.Type, x.Value }).IsUnique();
    }
}
