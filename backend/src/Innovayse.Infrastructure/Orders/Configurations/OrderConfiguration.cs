namespace Innovayse.Infrastructure.Orders.Configurations;

using Innovayse.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="Order"/>.</summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(50);
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();

        // 43 characters for the base64url of 32 random bytes; the width is rounded up so a
        // future change to the token length does not need a column migration as well.
        builder.Property(x => x.PaymentToken).IsRequired().HasMaxLength(64);
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(x => x.DomainEvents);
    }
}
