namespace Innovayse.Infrastructure.Email.Configurations;

using Innovayse.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="EmailDomain"/> aggregate.</summary>
public sealed class EmailDomainConfiguration : IEntityTypeConfiguration<EmailDomain>
{
    /// <summary>Configures the <c>email_domains</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<EmailDomain> builder)
    {
        builder.ToTable("email_domains");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.DomainName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MailcowRef).HasMaxLength(255);
        builder.Property(x => x.DkimSelector).HasMaxLength(50);
        builder.Property(x => x.DkimPublicKey).HasMaxLength(2048);
        builder.Property(x => x.MaxMailboxes).IsRequired();
        builder.Property(x => x.MaxAliases).IsRequired();
        builder.Property(x => x.MaxQuotaMb).IsRequired();
        builder.Property(x => x.DnsVerifiedAt);
        builder.Property(x => x.LastDnsVerificationJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.HostpanelDomainId);

        builder.HasIndex(x => x.DomainName).IsUnique();
        builder.HasIndex(x => x.ClientId);

        // Navigation: EmailDomain owns Mailboxes via private backing field _mailboxes.
        builder.HasMany(x => x.Mailboxes)
            .WithOne()
            .HasForeignKey(x => x.EmailDomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Mailboxes)
            .HasField("_mailboxes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: EmailDomain owns Aliases via private backing field _aliases.
        builder.HasMany(x => x.Aliases)
            .WithOne()
            .HasForeignKey(x => x.EmailDomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Aliases)
            .HasField("_aliases")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
