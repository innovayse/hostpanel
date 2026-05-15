namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Domain"/> aggregate.</summary>
public sealed class DomainConfiguration : IEntityTypeConfiguration<Domain>
{
    /// <summary>Configures the <c>domains</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Domain> builder)
    {
        builder.ToTable("domains");
        builder.HasKey(x => x.Id);

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

        builder.Property(x => x.RecurringAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(x => x.PriceCurrency).HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(x => x.NextDueDate);
        builder.Property(x => x.Registrar).HasMaxLength(100);
        builder.Property(x => x.RegistrationPeriod).HasDefaultValue(1);

        builder.Property(x => x.FirstPaymentAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.PromotionCode).HasMaxLength(100);
        builder.Property(x => x.SubscriptionId).HasMaxLength(255);
        builder.Property(x => x.AdminNotes).HasMaxLength(4000);
        builder.Property(x => x.OrderType).HasMaxLength(20).HasDefaultValue("Register");

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.ClientId);
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
    }
}
