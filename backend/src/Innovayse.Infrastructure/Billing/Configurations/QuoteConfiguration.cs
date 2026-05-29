namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Quote"/> aggregate.</summary>
public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
<<<<<<< HEAD
    /// <inheritdoc/>
=======
    /// <summary>Configures the <c>quotes</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
>>>>>>> origin/main
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
<<<<<<< HEAD
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExpiryDate).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // Navigation: Quote owns a collection of QuoteItems via private backing field _items.
=======
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.ValidUntil);
        builder.Property(x => x.SubTotal).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.ProposalText).HasMaxLength(10000);
        builder.Property(x => x.CustomerNotes).HasMaxLength(5000);
        builder.Property(x => x.AdminNotes).HasMaxLength(5000);

>>>>>>> origin/main
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
