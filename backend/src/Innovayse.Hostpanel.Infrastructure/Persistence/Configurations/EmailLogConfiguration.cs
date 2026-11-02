using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="EmailLog"/> entity.</summary>
public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    /// <summary>
    /// The name of the unique index that lets one delivery key be claimed once. The database context
    /// recognises a violation of it by this name, so the name is written once, here.
    /// </summary>
    public const string IdempotencyKeyIndex = "IX_EmailLogs_IdempotencyKey";

    /// <summary>Length of a delivery key: a SHA-256 digest as lower-case hex.</summary>
    private const int IdempotencyKeyLength = 64;

    /// <summary>The filter that limits <see cref="IdempotencyKeyIndex"/> to keyed rows; unkeyed sends repeat freely.</summary>
    private const string IdempotencyKeyFilter = "\"IdempotencyKey\" IS NOT NULL";

    /// <summary>Configures the <c>EmailLogs</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.To).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();
        builder.Property(x => x.Success).IsRequired();
        builder.Property(x => x.Failure).HasColumnName("Error").HasConversion(new EmailSendFailureConverter()).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(IdempotencyKeyLength);
        builder.Property(x => x.State).IsRequired();

        // Two deliveries of one keyed message may reclaim the same failed or stale row at once; the
        // row version lets only one of the two saves win, so only one of them sends.
        builder.UseXminConcurrencyToken();

        builder.HasIndex(x => x.IdempotencyKey, IdempotencyKeyIndex)
            .IsUnique()
            .HasFilter(IdempotencyKeyFilter);

        // A client's mail history is read by recipient address, newest first.
        builder.HasIndex(x => new { x.To, x.SentAt });
    }
}
