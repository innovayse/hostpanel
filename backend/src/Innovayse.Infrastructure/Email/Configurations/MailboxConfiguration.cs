namespace Innovayse.Infrastructure.Email.Configurations;

using Innovayse.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Mailbox"/> entity.</summary>
public sealed class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    /// <summary>Configures the <c>email_mailboxes</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("email_mailboxes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmailDomainId).IsRequired();
        builder.Property(x => x.LocalPart).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.QuotaMb).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.EmailDomainId, x.LocalPart }).IsUnique();
    }
}
