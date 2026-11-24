using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ClientService"/>.</summary>
public sealed class ClientServiceConfiguration : IEntityTypeConfiguration<ClientService>
{
    /// <summary>
    /// The name of the unique index on the order line a service was created for, the default EF already gave it, named
    /// here so <see cref="AppDbContext"/> can tell its violation from any other.
    /// </summary>
    public const string OrderItemIndex = "IX_ClientServices_OrderItemId";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientService> builder)
    {
        builder.HasKey(x => x.Id);

        // Two staff actions or a cron and a staff action that load the same service cannot both
        // save over it; the second save fails with ConcurrencyConflictException.
        builder.UseXminConcurrencyToken();

        // The client's service list, the renewal sweeps (Status, NextRenewalAt) and the status
        // counts the dashboard and reports run.
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => new { x.Status, x.NextRenewalAt });

        // How many services of a product are live: the count a product's cap and its one-per-client rule are checked
        // against on every order, and by the catalogue for every capped product.
        builder.HasIndex(x => new { x.ProductId, x.Status });

        // The trial sweeps: the active trials that are due to end or to be reminded.
        builder.HasIndex(x => new { x.TrialStatus, x.TrialEndsAt });

        // The source system's service id, kept for migration de-duplication. A source id is unique
        // only within one source, so it is unique per client, and only where one is set.
        builder.HasIndex(x => new { x.ClientId, x.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        // The service an order line created. At most one service exists per line, so a retry of the line reuses the
        // service its first attempt made instead of provisioning and billing a second one; the database enforces it.
        builder.HasIndex(x => x.OrderItemId)
            .IsUnique()
            .HasFilter("\"OrderItemId\" IS NOT NULL")
            .HasDatabaseName(OrderItemIndex);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.OrderItemId);
        builder.Property(x => x.BillingCycle).IsRequired().HasConversion(new BillingCycleConverter()).HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.TrialStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.TrialEndsAt);
        builder.Property(x => x.TrialAutoConvert).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.TrialGracePeriodDays).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.TrialReminderDays);
        builder.Property(x => x.FirstPaymentAmount).HasColumnType("numeric(18,4)");
        builder.Property(x => x.RecurringAmount).HasColumnType("numeric(18,4)");
        builder.Property(x => x.ProvisioningRef).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Domain).HasMaxLength(253);
        builder.Property(x => x.DedicatedIp).HasMaxLength(45);
        builder.Property(x => x.Username).HasMaxLength(100);

        // Password is encrypted and sized by ProtectedColumns.
        builder.Property(x => x.SuspendedAt);

        // Default true in the database too, so every existing service keeps being renewed. The
        // sentinel is true, not the CLR default false: with the default sentinel EF treats an
        // explicit false as "unset", omits the column and the database default silently stores true.
        builder.Property(x => x.AutoRenew).IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.SuspensionSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ServerId);
        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.PromotionCode).HasMaxLength(50);
        builder.Property(x => x.SubscriptionId).HasMaxLength(256);
        builder.Property(x => x.ExternalId).HasMaxLength(ClientService.MaxExternalIdLength);
        builder.Property(x => x.AutoTerminateReason).HasMaxLength(2000);
        builder.Property(x => x.AdminNotes).HasMaxLength(2000);

        // Every SSH key add moves SshKeyRevision in the same save as the key, so the add updates this row and the row's
        // xmin token (above) makes two adds of one service conflict; the loser recounts, so the per-service key limit
        // holds. An unrelated save of the same service in that window conflicts too, which the installer retries.
        builder.Property(x => x.SshKeyRevision).IsRequired().HasDefaultValue(0L);

        // The same serialisation for the per-service website and database limits: every change of a resource moves
        // HostingResourceRevision in the same save, so two concurrent changes of one service conflict on xmin.
        builder.Property(x => x.HostingResourceRevision).IsRequired().HasDefaultValue(0L);
        builder.Ignore(x => x.DomainEvents);

        // A client who still has this record cannot be deleted: billing, service and support history
        // is kept, so the database refuses the delete instead of leaving rows without an owner.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // A product that clients still hold services of cannot be deleted.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
