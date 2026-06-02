namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="DownloadCategory"/>.</summary>
public sealed class DownloadCategoryConfiguration : IEntityTypeConfiguration<DownloadCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DownloadCategory> builder)
    {
        builder.ToTable("download_categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.IsHidden).IsRequired();
        builder.Property(x => x.ParentCategoryId);

        builder.HasIndex(x => x.ParentCategoryId);
    }
}
