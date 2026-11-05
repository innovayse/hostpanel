using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="InvoicePaymentClaim"/> aggregate.</summary>
public sealed class InvoicePaymentClaimConfiguration : IEntityTypeConfiguration<InvoicePaymentClaim>
{
    /// <summary>
    /// The unique index that lets an invoice hold one blocking claim (an unresolved one, or one held for an operator).
    /// Named so the context can tell two charges started for one invoice at once (a violation of this index) from any
    /// other failure.
    /// </summary>
    public const string UnresolvedInvoiceIndex = "IX_InvoicePaymentClaims_InvoiceId_Blocking";

    /// <summary>The filter that limits <see cref="UnresolvedInvoiceIndex"/> to claims that block a new charge.</summary>
    private const string UnresolvedFilter = "\"Status\" IN ('Open', 'ActionRequired', 'Pending', 'Held')";

    /// <summary>Column width for an <c>InvoicePaymentClaimStatus</c> name stored as text.</summary>
    private const int StatusLength = 20;

    /// <summary>Configures the <c>InvoicePaymentClaims</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InvoicePaymentClaim> builder)
    {
        builder.HasKey(x => x.Id);

        // The charge path and the reconciliation pass can read the same claim; the second save loses.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayModuleId).IsRequired().HasMaxLength(GatewayCustomer.MaxLength);
        builder.Property(x => x.MethodRef).IsRequired().HasMaxLength(InvoicePaymentClaim.MaxLength);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(InvoicePaymentClaim.MaxKeyLength);
        builder.Property(x => x.AmountMinor).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(StatusLength);
        builder.Property(x => x.TransactionId).HasMaxLength(InvoicePaymentClaim.MaxLength);
        builder.Property(x => x.ActionUrl).HasMaxLength(InvoicePaymentClaim.MaxActionUrlLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CheckedAt);
        builder.Property(x => x.ResolvedAt);

        builder.Ignore(x => x.IsUnresolved);
        builder.Ignore(x => x.IsBlocking);

        // An invoice is charged once at a time: the database refuses a second blocking claim for it.
        builder.HasIndex(x => x.InvoiceId, UnresolvedInvoiceIndex)
            .IsUnique()
            .HasFilter(UnresolvedFilter);

        // One attempt, one key: a key is never reused by another claim.
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        // The reconciliation pass reads unresolved claims by age.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // The claim is the invoice's history of stored charges, so an invoice that has one is kept.
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
