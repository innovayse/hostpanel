namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="DnsRecord"/> entity.</summary>
public sealed class DnsRecordConfiguration : IEntityTypeConfiguration<DnsRecord>
{
    /// <summary>Configures the <c>dns_records</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DnsRecord> builder)
    {
        builder.ToTable("dns_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DomainId).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.Host).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Ttl).IsRequired();
        builder.Property(x => x.Priority);
    }
}
