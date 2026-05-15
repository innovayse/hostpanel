namespace Innovayse.Infrastructure.Support.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="TicketReply"/> entity.</summary>
public sealed class TicketReplyConfiguration : IEntityTypeConfiguration<TicketReply>
{
    /// <summary>Configures the <c>ticket_replies</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TicketReply> builder)
    {
        builder.ToTable("ticket_replies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.IsStaffReply).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property<int>("TicketId").IsRequired();
        builder.HasIndex("TicketId");
    }
}
