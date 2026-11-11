using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusComponentGroup"/>.</summary>
public sealed class StatusComponentGroupConfiguration : IEntityTypeConfiguration<StatusComponentGroup>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusComponentGroup> builder)
    {
        builder.HasKey(x => x.Id);
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(StatusComponentGroup.NameMaxLength);
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.HasIndex(x => x.DisplayOrder);

        builder.HasMany(x => x.Components)
            .WithOne()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Components).AutoInclude();
    }
}
