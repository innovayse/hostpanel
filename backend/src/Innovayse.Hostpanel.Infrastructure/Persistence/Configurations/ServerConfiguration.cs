using Innovayse.Hostpanel.Domain.Servers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="Server"/>.</summary>
public sealed class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Server> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Hostname).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ApiHost).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.AssignedIpAddresses).HasMaxLength(2000);
        builder.Property(x => x.Module).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(200);

        // Password, ApiToken and AccessHash are encrypted and sized by ProtectedColumns.

        builder.Property(x => x.UseSSL).IsRequired();
        builder.Property(x => x.AcceptsSelfSignedCertificate).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.MaxAccounts);
        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.IsDisabled).IsRequired();
        builder.Property(x => x.MonthlyCost).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.Datacenter).HasMaxLength(200);
        builder.Property(x => x.ServerStatusAddress).HasMaxLength(500);
        builder.Property(x => x.Ns1Hostname).HasMaxLength(200);
        builder.Property(x => x.Ns1Ip).HasMaxLength(45);
        builder.Property(x => x.Ns2Hostname).HasMaxLength(200);
        builder.Property(x => x.Ns2Ip).HasMaxLength(45);
        builder.Property(x => x.Ns3Hostname).HasMaxLength(200);
        builder.Property(x => x.Ns3Ip).HasMaxLength(45);
        builder.Property(x => x.Ns4Hostname).HasMaxLength(200);
        builder.Property(x => x.Ns4Ip).HasMaxLength(45);
        builder.Property(x => x.Ns5Hostname).HasMaxLength(200);
        builder.Property(x => x.Ns5Ip).HasMaxLength(45);
        builder.Property(x => x.ServerGroupId);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
