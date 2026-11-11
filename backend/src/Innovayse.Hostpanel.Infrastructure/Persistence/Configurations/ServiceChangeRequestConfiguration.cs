using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ServiceChangeRequest"/>.</summary>
public sealed class ServiceChangeRequestConfiguration : IEntityTypeConfiguration<ServiceChangeRequest>
{
    /// <summary>
    /// The unique index that lets a service hold one open plan change request (Pending or Approved). Named so the context
    /// can tell two requests raised for one service at once (a violation of this index) from any other failure.
    /// </summary>
    public const string OpenRequestIndex = "IX_ServiceChangeRequests_ServiceId_Open";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ServiceChangeRequest> builder)
    {
        builder.HasKey(x => x.Id);

        // Staff approving a request while the payment of its invoice applies it, or two staff approving it, cannot both
        // complete it (and credit a downgrade twice): the second save fails with ConcurrencyConflictException.
        builder.UseXminConcurrencyToken();

        // A service's plan change history, and the staff list of requests in one status (the pending queue).
        builder.HasIndex(x => x.ServiceId);
        builder.HasIndex(x => x.Status);

        // Two requests raised for one service at once would charge or credit the same billing cycle twice; the handler's
        // check cannot see the other in-flight request, so the database refuses the second.
        builder.HasIndex(x => x.ServiceId, OpenRequestIndex)
            .IsUnique()
            .HasFilter($"\"Status\" IN ('{nameof(ServiceChangeStatus.Pending)}', '{nameof(ServiceChangeStatus.Approved)}')");

        builder.Property(x => x.ServiceId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.FromProductId).IsRequired();
        builder.Property(x => x.ToProductId).IsRequired();
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProrationAmount).HasColumnType("numeric(18,4)");
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(x => x.NewRecurringAmount).HasColumnType("numeric(18,4)");

        // The invoice belongs to Billing and the request must survive it: an index only, no foreign key.
        builder.Property(x => x.InvoiceId);
        builder.HasIndex(x => x.InvoiceId);

        builder.Property(x => x.Note).HasMaxLength(ServiceChangeRequest.MaxNoteLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ResolvedAt);

        // A plan change request has no meaning without its service.
        builder.HasOne<ClientService>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // History is kept: a client or a product that a request names cannot be deleted.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.FromProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ToProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
