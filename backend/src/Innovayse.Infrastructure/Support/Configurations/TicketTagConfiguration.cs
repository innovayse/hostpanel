namespace Innovayse.Infrastructure.Support.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="TicketTag"/> entity.</summary>
public sealed class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    /// <summary>Configures the <c>ticket_tags</c> table mapping.</summary>
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("ticket_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.TicketId, x.Name }).IsUnique();
        builder.HasIndex(x => x.Name);
    }
}
