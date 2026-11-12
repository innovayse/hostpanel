using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="Order"/>.</summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        // Two staff actions on the same order (accept, cancel, fraud decision) cannot both save over
        // each other; the second save fails with ConcurrencyConflictException.
        builder.UseXminConcurrencyToken();

        // The client's orders, and the status-filtered newest-first order list.
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        builder.Property(x => x.OrderNumber).IsRequired().HasMaxLength(Order.OrderNumberMaxLength);
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(50);
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.BundleId);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.FraudScore);
        builder.Property(x => x.FraudReasonCodes).IsRequired().HasMaxLength(FraudReasonText.MaxLength);
        builder.Ignore(x => x.FraudReasons);
        builder.Property(x => x.CreatedAt).IsRequired();

        // 43 characters for the base64url of 32 random bytes; the width is rounded up so a
        // future change to the token length does not need a column migration as well.
        builder.Property(x => x.PaymentToken).IsRequired().HasMaxLength(64);
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(x => x.DomainEvents);

        // A client who still has this record cannot be deleted: billing, service and support history
        // is kept, so the database refuses the delete instead of leaving rows without an owner.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // An order outlives its invoice: deleting the invoice clears the link.
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // An order outlives the bundle it was placed from: deleting the bundle clears the link.
        builder.HasOne<ProductBundle>()
            .WithMany()
            .HasForeignKey(x => x.BundleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.BundleId);
    }
}
