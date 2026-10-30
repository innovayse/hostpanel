using Innovayse.Hostpanel.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="LatestRelease"/>.</summary>
public sealed class LatestReleaseConfiguration : IEntityTypeConfiguration<LatestRelease>
{
    /// <summary>Configures the <c>LatestReleases</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<LatestRelease> builder)
    {
        builder.HasKey(x => x.Id);

        // The id is set by the factory, not the database sequence.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ReleaseNotesUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PublishedAt).IsRequired();
        builder.Property(x => x.CheckedAt).IsRequired();
    }
}
