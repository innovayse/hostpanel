namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

<<<<<<< HEAD
/// <summary>EF Core configuration for the <see cref="BillableItem"/> entity.</summary>
public sealed class BillableItemConfiguration : IEntityTypeConfiguration<BillableItem>
{
    /// <inheritdoc/>
=======
/// <summary>EF Core configuration for the <see cref="BillableItem"/> aggregate.</summary>
public sealed class BillableItemConfiguration : IEntityTypeConfiguration<BillableItem>
{
    /// <summary>Configures the <c>billable_items</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
>>>>>>> origin/main
    public void Configure(EntityTypeBuilder<BillableItem> builder)
    {
        builder.ToTable("billable_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
<<<<<<< HEAD
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RecurringPeriod).HasMaxLength(50);
        builder.Property(x => x.IsInvoiced).IsRequired();
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.NextDueDate);
        builder.Property(x => x.CreatedAt).IsRequired();
=======
        builder.Property(x => x.ServiceId);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.HoursQty).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.IsHours).IsRequired();
        builder.Property(x => x.InvoiceAction).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.InvoiceCount).IsRequired();
        builder.Property(x => x.RecurrenceInterval);
        builder.Property(x => x.RecurrencePeriod).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RecurrenceLimit);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.ClientId, x.InvoiceId })
            .HasDatabaseName("IX_billable_items_ClientId_InvoiceId");
>>>>>>> origin/main
    }
}
