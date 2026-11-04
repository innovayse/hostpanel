using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="GatewayCustomer"/>.</summary>
public sealed class GatewayCustomerConfiguration : IEntityTypeConfiguration<GatewayCustomer>
{
    /// <summary>
    /// The name of the unique index that lets a client have one account per gateway module. The database context
    /// recognises a violation of it by this name, so the name is written once, here.
    /// </summary>
    public const string ClientModuleIndex = "IX_GatewayCustomers_ClientId_GatewayModuleId";

    /// <summary>Column width of a payment gateway module id.</summary>
    private const int ModuleIdLength = 100;

    /// <summary>Column width of a gateway's customer reference.</summary>
    private const int CustomerRefLength = 255;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GatewayCustomer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayModuleId).IsRequired().HasMaxLength(ModuleIdLength);
        builder.Property(x => x.CustomerRef).IsRequired().HasMaxLength(CustomerRefLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        // One account per client and gateway: of two requests creating it at once, the database lets one insert win.
        builder.HasIndex(x => new { x.ClientId, x.GatewayModuleId }, ClientModuleIndex).IsUnique();

        // One gateway account belongs to one client only, whatever the application did.
        builder.HasIndex(x => new { x.GatewayModuleId, x.CustomerRef }).IsUnique();

        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}
