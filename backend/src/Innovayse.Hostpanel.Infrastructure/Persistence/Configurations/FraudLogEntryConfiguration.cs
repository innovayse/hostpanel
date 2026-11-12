using Innovayse.Hostpanel.Domain.Fraud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="FraudLogEntry"/>.</summary>
public sealed class FraudLogEntryConfiguration : IEntityTypeConfiguration<FraudLogEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FraudLogEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Decision).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(FraudLogEntry.IpAddressMaxLength);
        builder.Property(x => x.Email).HasMaxLength(FraudLogEntry.EmailMaxLength);
        builder.Property(x => x.Score).IsRequired();
        builder.Property(x => x.ReasonCodes).IsRequired().HasMaxLength(FraudReasonText.MaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.Reasons);
        builder.Ignore(x => x.DomainEvents);

        // The log is a record of what was decided at the time: it keeps no foreign key, so deleting an order or a client
        // never removes the evidence of why it was refused or held.

        // The velocity and shared account counts read one address's recent rows.
        builder.HasIndex(x => new { x.IpAddress, x.CreatedAt });

        // The admin log is newest first and filtered by decision.
        builder.HasIndex(x => new { x.Decision, x.CreatedAt });
    }
}
