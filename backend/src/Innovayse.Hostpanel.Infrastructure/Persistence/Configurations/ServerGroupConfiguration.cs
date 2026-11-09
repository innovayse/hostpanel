using Innovayse.Hostpanel.Domain.Servers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="ServerGroup"/>.</summary>
public sealed class ServerGroupConfiguration : IEntityTypeConfiguration<ServerGroup>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ServerGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.FillType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Servers)
            .WithOne()
            .HasForeignKey(s => s.ServerGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
