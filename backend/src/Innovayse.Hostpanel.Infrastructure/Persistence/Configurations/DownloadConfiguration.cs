using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="Download"/>.</summary>
public sealed class DownloadConfiguration : IEntityTypeConfiguration<Download>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Download> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Filename).HasMaxLength(500);
        builder.Property(x => x.CategoryId).IsRequired();
        builder.Property(x => x.DownloadCount).IsRequired();
        builder.Property(x => x.ClientsOnly).IsRequired();
        builder.Property(x => x.ProductDownload).IsRequired();
        builder.Property(x => x.IsHidden).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // A category that still holds downloads cannot be deleted; the delete use case refuses it first.
        builder.HasOne<DownloadCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serves the category lookup and the migration import find-by-title within one category.
        builder.HasIndex(x => new { x.CategoryId, x.Title });
    }
}
