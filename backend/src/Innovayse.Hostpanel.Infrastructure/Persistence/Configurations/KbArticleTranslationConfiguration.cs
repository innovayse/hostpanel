using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="KbArticleTranslation"/>.</summary>
public sealed class KbArticleTranslationConfiguration : IEntityTypeConfiguration<KbArticleTranslation>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<KbArticleTranslation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArticleId).IsRequired();
        builder.Property(x => x.Locale).IsRequired().HasMaxLength(KbArticleTranslation.LocaleMaxLength);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(KbArticleTranslation.TitleMaxLength);
        builder.Property(x => x.Content).IsRequired();

        builder.HasIndex(x => new { x.ArticleId, x.Locale }).IsUnique();
    }
}
