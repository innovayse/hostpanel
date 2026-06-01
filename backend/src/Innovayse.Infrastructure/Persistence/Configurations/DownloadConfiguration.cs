namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="Download"/>.</summary>
public sealed class DownloadConfiguration : IEntityTypeConfiguration<Download>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Download> builder)
    {
        builder.ToTable("downloads");
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

        builder.HasOne<DownloadCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CategoryId);
    }
}
