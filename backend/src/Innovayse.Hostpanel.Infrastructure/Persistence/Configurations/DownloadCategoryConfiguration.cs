using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="DownloadCategory"/>.</summary>
public sealed class DownloadCategoryConfiguration : IEntityTypeConfiguration<DownloadCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DownloadCategory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.IsHidden).IsRequired();
        builder.Property(x => x.ParentCategoryId);

        // Serves the parent lookup and the migration import find-by-name within one parent.
        builder.HasIndex(x => new { x.ParentCategoryId, x.Name });

        // A category with sub-categories cannot be deleted; the delete use case refuses it first.
        builder.HasOne<DownloadCategory>()
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
