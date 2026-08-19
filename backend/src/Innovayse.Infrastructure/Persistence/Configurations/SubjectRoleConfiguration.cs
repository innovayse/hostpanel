namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="SubjectRole"/>.</summary>
public sealed class SubjectRoleConfiguration : IEntityTypeConfiguration<SubjectRole>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SubjectRole> builder)
    {
        builder.ToTable("subject_roles");

        // The pair is the key, which is what makes a repeated grant a no-op at the
        // database rather than something every caller has to remember to check for.
        builder.HasKey(x => new { x.Subject, x.Role });

        // Wide enough for an SSO subject or a local Identity id, both of which are
        // strings this product does not generate and cannot shorten.
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(64);

        // Every authorization check is "what does this subject hold", so the leading
        // key column already serves it; the index exists for the reverse question,
        // "who holds Admin", which the admin screens ask.
        builder.HasIndex(x => x.Role);
    }
}
