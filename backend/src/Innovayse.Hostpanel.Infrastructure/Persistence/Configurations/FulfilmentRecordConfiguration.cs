using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="FulfilmentRecord"/> aggregate.</summary>
public sealed class FulfilmentRecordConfiguration : IEntityTypeConfiguration<FulfilmentRecord>
{
    /// <summary>Column width for the plugin id a fulfilment plugin declares for itself.</summary>
    private const int PluginIdLength = 100;

    /// <summary>Column width for a <c>FulfilmentKind</c> or <c>FulfilmentStatus</c> name stored as text.</summary>
    private const int EnumNameLength = 20;

    /// <summary>Configures the <c>FulfilmentRecords</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<FulfilmentRecord> builder)
    {
        builder.HasKey(x => x.Id);

        // Two dispatchers that read the same due record cannot both claim it: the second save fails with
        // a concurrency conflict, so one plugin call is made per claim.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.PluginId).IsRequired().HasMaxLength(PluginIdLength);
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(FulfilmentRecord.MaxErrorLength);
        builder.Property(x => x.CompletedAt);
        builder.Property(x => x.CreatedAt).IsRequired();

        // One plugin hears about one thing that happened to one invoice once: a payment, refund or lapse
        // that is reported twice finds the row already there instead of writing a second delivery.
        builder.HasIndex(x => new { x.InvoiceId, x.PluginId, x.Kind }).IsUnique();

        // The dispatcher's read: due records by status and due time (FulfilmentRecord.DueFilter).
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });

        // The record is the invoice's history of what plugins were told, so an invoice that has one is
        // kept: the database refuses the delete rather than lose what was delivered for it.
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
