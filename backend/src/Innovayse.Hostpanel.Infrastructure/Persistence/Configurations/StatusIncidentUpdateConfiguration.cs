using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusIncidentUpdate"/>.</summary>
public sealed class StatusIncidentUpdateConfiguration : IEntityTypeConfiguration<StatusIncidentUpdate>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusIncidentUpdate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncidentId).IsRequired();
        builder.Property(x => x.Message).IsRequired().HasMaxLength(StatusIncidentUpdate.MessageMaxLength);
        builder.Property(x => x.State).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.IncidentId, x.CreatedAt });
    }
}
