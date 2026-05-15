namespace Innovayse.Infrastructure.Services.Configurations;

using Innovayse.Domain.Services;
using Innovayse.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="ClientService"/>.</summary>
public sealed class ClientServiceConfiguration : IEntityTypeConfiguration<ClientService>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientService> builder)
    {
        builder.ToTable("client_services");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.BillingCycle).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
        builder.Property(x => x.ProvisioningRef).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Domain).HasMaxLength(253);
        builder.Property(x => x.DedicatedIp).HasMaxLength(45);
        builder.Property(x => x.Username).HasMaxLength(100);

        var converter = EncryptionServiceHolder.CreateConverter();
        if (converter is not null)
        {
            builder.Property(x => x.Password).HasConversion(converter).HasMaxLength(512);
        }
        else
        {
            builder.Property(x => x.Password).HasMaxLength(256);
        }

        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.PromotionCode).HasMaxLength(50);
        builder.Property(x => x.SubscriptionId).HasMaxLength(256);
        builder.Property(x => x.AutoTerminateReason).HasMaxLength(2000);
        builder.Property(x => x.AdminNotes).HasMaxLength(2000);
        builder.Ignore(x => x.DomainEvents);
    }
}
