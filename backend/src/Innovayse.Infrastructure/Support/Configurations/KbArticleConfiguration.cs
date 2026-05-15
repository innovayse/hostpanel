namespace Innovayse.Infrastructure.Support.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="KbArticle"/> entity.</summary>
public sealed class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    /// <summary>Configures the <c>kb_articles</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        builder.ToTable("kb_articles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Category).IsRequired().HasMaxLength(255);
        builder.Property(x => x.IsPublished).IsRequired();

        builder.HasIndex(x => x.IsPublished);
        builder.HasIndex(x => x.Category);
    }
}
