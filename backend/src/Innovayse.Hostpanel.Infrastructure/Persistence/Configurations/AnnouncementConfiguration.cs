using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="Announcement"/>.</summary>
public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.IsPublished)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Natural key the migration import looks an announcement up by (date first, the title is compared lower-cased); not unique, history may repeat a title.
        builder.HasIndex(x => new { x.CreatedAt, x.Title });
    }
}
