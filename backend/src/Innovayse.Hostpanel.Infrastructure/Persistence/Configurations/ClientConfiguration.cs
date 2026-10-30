using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table configuration for <see cref="Client"/>.</summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(x => x.Id);

        // Two writers that both read the credit balance cannot both save over it, the
        // second save fails with ConcurrencyConflictException instead of losing the first change.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Street).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.PostCode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(2);

        builder.Property(x => x.Address2).HasMaxLength(200);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.CardGatewayCustomerId).HasMaxLength(50);

        // The link to an account in another system is whole or absent, which the domain keeps and the
        // check enforces for every writer. One external account belongs to one client only, so the pair is
        // unique; clients without a link stay out of the index.
        builder.Property(x => x.ExternalAccountProvider).HasMaxLength(Client.ExternalAccountProviderMaxLength);
        builder.Property(x => x.ExternalAccountId).HasMaxLength(Client.ExternalAccountIdMaxLength);
        builder.HasIndex(x => new { x.ExternalAccountProvider, x.ExternalAccountId })
            .IsUnique()
            .HasFilter("\"ExternalAccountProvider\" IS NOT NULL AND \"ExternalAccountId\" IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Clients_ExternalAccount",
            "(\"ExternalAccountProvider\" IS NULL) = (\"ExternalAccountId\" IS NULL)"));
        builder.Property(x => x.BillingContact).HasMaxLength(256);
        builder.Property(x => x.AdminNotes).HasMaxLength(2000);

        builder.Property(x => x.CreditBalance).HasColumnType("numeric(18,4)").HasDefaultValue(0m);

        builder.Property(x => x.CreatedAt).IsRequired();

        // Client has no late fee flag; the column is mapped as a shadow property so it stays in the table and
        // the migration is additive. A later cleanup drops the column and this shadow property together.
        builder.Property<bool>("LateFees").HasColumnName("LateFees").HasColumnType("boolean");

        builder.Property(x => x.RegistrationIp).HasMaxLength(45);
        builder.Property(x => x.RegistrationUserAgent).HasMaxLength(500);
        builder.Property(x => x.DeviceType).HasMaxLength(20);
        builder.Property(x => x.OperatingSystem).HasMaxLength(50);
        builder.Property(x => x.Browser).HasMaxLength(50);

        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Contacts)
            .WithOne()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Contacts).HasField("_contacts");

        builder.HasMany(x => x.Users)
            .WithOne()
            .HasForeignKey(u => u.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Users).HasField("_users");
    }
}
