using Innovayse.Hostpanel.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="ActivityRead"/>.</summary>
public sealed class ActivityReadConfiguration : IEntityTypeConfiguration<ActivityRead>
{
    /// <summary>Configures the <c>ActivityReads</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ActivityRead> builder)
    {
        builder.HasKey(x => new { x.UserId, x.EventKey });

        builder.Property(x => x.UserId).HasMaxLength(200);
        builder.Property(x => x.EventKey).HasMaxLength(ActivityRead.MaxEventKeyLength);

        // The feed reads every row of one admin at once; the key's leading column already serves it, the index
        // keeps that read independent of the key's column order.
        builder.HasIndex(x => x.UserId);
    }
}
