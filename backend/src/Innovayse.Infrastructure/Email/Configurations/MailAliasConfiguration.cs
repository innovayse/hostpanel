namespace Innovayse.Infrastructure.Email.Configurations;

using Innovayse.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="MailAlias"/> entity.</summary>
public sealed class MailAliasConfiguration : IEntityTypeConfiguration<MailAlias>
{
    /// <summary>Configures the <c>email_aliases</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<MailAlias> builder)
    {
        builder.ToTable("email_aliases");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmailDomainId).IsRequired();
        builder.Property(x => x.SourceAddress).IsRequired().HasMaxLength(255);
        builder.Property(x => x.DestinationAddress).IsRequired().HasMaxLength(255);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
