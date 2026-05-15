namespace Innovayse.Infrastructure.Services.Configurations;

using Innovayse.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="CancellationRequest"/>.</summary>
public sealed class CancellationRequestConfiguration : IEntityTypeConfiguration<CancellationRequest>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CancellationRequest> builder)
    {
        builder.ToTable("cancellation_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
