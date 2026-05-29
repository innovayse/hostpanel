namespace Innovayse.Infrastructure.Orders.Configurations;

using Innovayse.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="OrderItem"/>.</summary>
public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.BillingCycle).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Domain).HasMaxLength(253);
        builder.Property(x => x.Hostname).HasMaxLength(253);
        builder.Property(x => x.FirstPaymentAmount).IsRequired();
        builder.Property(x => x.RecurringAmount).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
    }
}
