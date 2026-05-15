namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for <see cref="ClientUser"/>.</summary>
public sealed class ClientUserConfiguration : IEntityTypeConfiguration<ClientUser>
{
    /// <summary>Configures the <c>client_users</c> table schema.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ClientUser> builder)
    {
        builder.ToTable("client_users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Permissions).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ClientId }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ClientId);
    }
}
