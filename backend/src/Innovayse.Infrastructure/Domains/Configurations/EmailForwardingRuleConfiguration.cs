namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="EmailForwardingRule"/> entity.
/// Maps to the <c>email_forwarding_rules</c> table.
/// </summary>
internal sealed class EmailForwardingRuleConfiguration : IEntityTypeConfiguration<EmailForwardingRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailForwardingRule> builder)
    {
        builder.ToTable("email_forwarding_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DomainId).IsRequired();
        builder.Property(r => r.Source).IsRequired().HasMaxLength(255);
        builder.Property(r => r.Destination).IsRequired().HasMaxLength(320);
        builder.Property(r => r.IsActive).HasDefaultValue(true);
        builder.HasIndex(r => r.DomainId);
    }
}
