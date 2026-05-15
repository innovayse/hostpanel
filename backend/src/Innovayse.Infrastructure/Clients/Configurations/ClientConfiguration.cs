namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="Client"/>.</summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(x => x.Id);

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
        builder.Property(x => x.BillingContact).HasMaxLength(256);
        builder.Property(x => x.AdminNotes).HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();

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
