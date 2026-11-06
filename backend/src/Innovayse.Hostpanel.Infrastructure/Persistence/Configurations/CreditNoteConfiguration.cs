using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="CreditNote"/> aggregate.</summary>
public sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    /// <summary>Column width for the note's number.</summary>
    private const int NumberLength = 30;

    /// <summary>Column width for a gateway refund reference.</summary>
    private const int ReferenceLength = 255;

    /// <summary>Configures the <c>CreditNotes</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.HasKey(x => x.Id);

        // Two voids, or a void and a read-modify-write of the same note, cannot both save over each other.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.Number).HasMaxLength(NumberLength);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(x => x.TaxRate).HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(x => x.SubTotal).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Tax).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(CreditNote.MaxReasonLength);
        builder.Property(x => x.RefundReference).HasMaxLength(ReferenceLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IssuedAt);
        builder.Property(x => x.AppliedAt);
        builder.Property(x => x.VoidedAt);
        builder.Property(x => x.VoidReason).HasMaxLength(CreditNote.MaxReasonLength);

        // A number belongs to one note for good. A draft has none yet, so only a set number is unique.
        builder.HasIndex(x => x.Number)
            .IsUnique()
            .HasFilter("\"Number\" IS NOT NULL");

        // The client's notes, and the notes of one invoice.
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.InvoiceId);

        // Navigation: the note owns its lines through the private backing field _items.
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.CreditNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // A credit note is a document that outlives everything it points to, so the database refuses to delete the
        // invoice or the client of one instead of leaving a note without an owner.
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
