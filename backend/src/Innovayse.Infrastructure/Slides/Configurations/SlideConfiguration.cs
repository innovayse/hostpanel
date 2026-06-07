namespace Innovayse.Infrastructure.Slides.Configurations;

using Innovayse.Domain.Slides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="Slide"/>.</summary>
public sealed class SlideConfiguration : IEntityTypeConfiguration<Slide>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Slide> builder)
    {
        builder.ToTable("slides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IconName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.BrandColor).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DemoUrl).HasMaxLength(500);
        builder.Property(x => x.LearnMoreUrl).HasMaxLength(500);
        builder.Property(x => x.ProductId);
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.TargetAudience)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.VisibleFrom);
        builder.Property(x => x.VisibleUntil);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.IsActive);
        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(x => x.SlideId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.Translations).AutoInclude();
    }
}
