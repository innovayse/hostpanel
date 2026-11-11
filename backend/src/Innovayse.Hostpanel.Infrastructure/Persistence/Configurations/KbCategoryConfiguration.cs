using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="KbCategory"/>.</summary>
public sealed class KbCategoryConfiguration : IEntityTypeConfiguration<KbCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KbCategory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(KbSlugRules.MaxLength);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.IsHidden).IsRequired();
        builder.Property(x => x.ParentCategoryId);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Slug).IsUnique();

        // Same slug rules as articles, without the reserved words: no route takes a category slug as a segment.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_KbCategories_SlugShape", $"\"Slug\" ~ '{KbSlugRules.DatabasePattern}'");
            table.HasCheckConstraint("CK_KbCategories_SlugNotNumeric", $"\"Slug\" !~ '{KbSlugRules.DatabaseNumericPattern}'");
        });

        // A category with sub-categories cannot be deleted; the delete use case refuses it first.
        builder.HasOne<KbCategory>()
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
