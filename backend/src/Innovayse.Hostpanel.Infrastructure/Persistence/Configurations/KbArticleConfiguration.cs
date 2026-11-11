using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="KbArticle"/>.</summary>
public sealed class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryId).IsRequired();
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(KbSlugRules.MaxLength);
        builder.Property(x => x.IsPublished).IsRequired();

        builder.HasIndex(x => x.IsPublished);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Slug).IsUnique();

        // The slug rules live in the database as well as in the domain, so no writer can store a slug that two
        // articles share without regard to case (the shape check admits lower case only), a number that reads as an
        // id, or a word the article routes use as a literal segment. The reserved words repeat
        // KbSlugRules.ReservedArticleSlugs.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_KbArticles_SlugShape", $"\"Slug\" ~ '{KbSlugRules.DatabasePattern}'");
            table.HasCheckConstraint("CK_KbArticles_SlugNotNumeric", $"\"Slug\" !~ '{KbSlugRules.DatabaseNumericPattern}'");
            table.HasCheckConstraint(
                "CK_KbArticles_SlugNotReserved",
                $"\"Slug\" NOT IN ({string.Join(", ", KbSlugRules.ReservedArticleSlugs.Select(word => $"'{word}'"))})");
        });

        // A category that still has articles cannot be deleted: the delete use case refuses it first.
        builder.HasOne<KbCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Translations).AutoInclude();
    }
}
