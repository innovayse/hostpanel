using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="GatewayCustomer"/> aggregate.</summary>
public sealed class GatewayCustomerConfiguration : IEntityTypeConfiguration<GatewayCustomer>
{
    /// <summary>
    /// The unique index on client and gateway module. Named so the context can tell two first requests for the same
    /// customer (a violation of this index) from any other failure.
    /// </summary>
    public const string ClientModuleIndex = "IX_GatewayCustomers_ClientId_GatewayModuleId";

    /// <summary>Configures the <c>GatewayCustomers</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<GatewayCustomer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayModuleId).IsRequired().HasMaxLength(GatewayCustomer.MaxLength);
        builder.Property(x => x.CustomerRef).IsRequired().HasMaxLength(GatewayCustomer.MaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        // A client has one customer per gateway module; two first requests at once cannot both create it.
        builder.HasIndex(x => new { x.ClientId, x.GatewayModuleId }, ClientModuleIndex).IsUnique();

        // A client who still has a gateway customer cannot be deleted: the gateway holds stored methods for it.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
