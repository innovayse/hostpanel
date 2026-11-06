using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="CreditNoteItem"/> owned entity.</summary>
public sealed class CreditNoteItemConfiguration : IEntityTypeConfiguration<CreditNoteItem>
{
    /// <summary>Column width for a line description.</summary>
    private const int DescriptionLength = 500;

    /// <summary>Configures the <c>CreditNoteItems</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<CreditNoteItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreditNoteId).IsRequired();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(DescriptionLength);
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
    }
}
