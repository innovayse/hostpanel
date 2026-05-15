namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for <see cref="Invitation"/>.</summary>
public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <summary>Configures the <c>invitations</c> table schema.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Permissions).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.ClientId);

        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsAccepted);
    }
}
