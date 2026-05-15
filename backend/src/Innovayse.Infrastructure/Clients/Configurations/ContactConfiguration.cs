namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="Contact"/>.</summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(50);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Street).HasMaxLength(200);
        builder.Property(x => x.Address2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.PostCode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(2);

        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
