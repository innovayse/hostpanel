namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Invoice"/> aggregate.</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <summary>Configures the <c>invoices</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.PaidAt);
        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(255);

        // Navigation: Invoice owns a collection of InvoiceItems via private backing field _items.
        // PropertyAccessMode.Field ensures EF reads/writes _items directly (readonly-safe).
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
