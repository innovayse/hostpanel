namespace Innovayse.Infrastructure.Slides.Configurations;

using Innovayse.Domain.Slides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="SlideTranslation"/>.</summary>
public sealed class SlideTranslationConfiguration : IEntityTypeConfiguration<SlideTranslation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SlideTranslation> builder)
    {
        builder.ToTable("slide_translations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SlideId).IsRequired();
        builder.Property(x => x.Locale).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Tagline).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Features).HasColumnType("text");
        builder.Property(x => x.CtaText).HasMaxLength(100);
        builder.Property(x => x.CtaUrl).HasMaxLength(500);
        builder.HasIndex(x => new { x.SlideId, x.Locale }).IsUnique();
    }
}
