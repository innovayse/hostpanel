using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Domain"/> aggregate.</summary>
public sealed class DomainConfiguration : IEntityTypeConfiguration<RegisteredDomain>
{
    /// <summary>Configures the <c>Domains</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RegisteredDomain> builder)
    {
        builder.HasKey(x => x.Id);

        // A registrar callback, the expiry sweep and a staff edit that load the same domain cannot
        // all save over each other; the later save fails with ConcurrencyConflictException.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Tld).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RegisteredAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.AutoRenew).IsRequired();
        builder.Property(x => x.WhoisPrivacy).IsRequired();
        builder.Property(x => x.IsLocked).IsRequired();
        builder.Property(x => x.RegistrarRef).HasMaxLength(100);
        builder.Property(x => x.EppCode).HasMaxLength(50);
        builder.Property(x => x.LinkedServiceId);

        builder.Property(x => x.RecurringAmount).HasColumnType("numeric(18,4)");
        builder.Property(x => x.PriceCurrency).HasMaxLength(3);
        builder.Property(x => x.NextDueDate);
        builder.Property(x => x.Registrar).HasMaxLength(100);
        builder.Property(x => x.RegistrarModule).HasConversion<string>().HasMaxLength(30);

        builder.Property(x => x.FirstPaymentAmount).HasColumnType("numeric(18,4)");
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.PromotionCode).HasMaxLength(100);
        builder.Property(x => x.SubscriptionId).HasMaxLength(255);
        builder.Property(x => x.AdminNotes).HasMaxLength(4000);
        builder.Property(x => x.OrderType).HasMaxLength(20).HasDefaultValue("Register");

        // The order line whose renewal last extended the domain, so a retry of that line does not extend it twice.
        builder.Property(x => x.LastRenewalOrderItemId);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        // The domains report filters by expiry with no status, which the composite above cannot serve.
        builder.HasIndex(x => x.ExpiresAt);

        // Navigation: Domain owns a collection of Nameservers via private backing field _nameservers.
        builder.HasMany(x => x.Nameservers)
            .WithOne()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Nameservers)
            .HasField("_nameservers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: Domain owns a collection of DnsRecords via private backing field _dnsRecords.
        builder.HasMany(x => x.DnsRecords)
            .WithOne()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.DnsRecords)
            .HasField("_dnsRecords")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: Domain owns a collection of EmailForwardingRules via private backing field _emailForwardingRules.
        builder.HasMany(x => x.EmailForwardingRules)
            .WithOne()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.EmailForwardingRules)
            .HasField("_emailForwardingRules")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: Domain owns a collection of Reminders via private backing field _reminders.
        builder.HasMany(x => x.Reminders)
            .WithOne()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Reminders)
            .HasField("_reminders")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // A client who still has this record cannot be deleted: billing, service and support history
        // is kept, so the database refuses the delete instead of leaving rows without an owner.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // A domain outlives the hosting service it was ordered with: deleting the service clears the link.
        builder.HasOne<ClientService>()
            .WithMany()
            .HasForeignKey(x => x.LinkedServiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
