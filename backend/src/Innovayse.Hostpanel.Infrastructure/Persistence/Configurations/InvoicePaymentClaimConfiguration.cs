using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="InvoicePaymentClaim"/>.</summary>
public sealed class InvoicePaymentClaimConfiguration : IEntityTypeConfiguration<InvoicePaymentClaim>
{
    /// <summary>Column width of an enum name stored as text.</summary>
    private const int EnumNameLength = 16;

    /// <summary>Column width of the gateway idempotency key.</summary>
    private const int KeyLength = 100;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InvoicePaymentClaim> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.GatewayIdempotencyKey).IsRequired().HasMaxLength(KeyLength);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(EnumNameLength);

        // The whole point of the table: only one unfinished claim per invoice can exist, whoever asks and
        // however many at once. A second insert is refused by the database, not by a check that could race.
        builder.HasIndex(x => x.InvoiceId)
            .IsUnique()
            .HasFilter("\"Outcome\" IS NULL");

        // Not unique: a client who retries a declined payment attempt under the same attempt id starts a
        // new claim with the same key, and the gateway answers it with the attempt's first outcome.
        builder.HasIndex(x => x.GatewayIdempotencyKey);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
