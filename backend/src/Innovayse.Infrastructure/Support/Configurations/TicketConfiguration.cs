namespace Innovayse.Infrastructure.Support.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Ticket"/> aggregate.</summary>
public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    /// <summary>Configures the <c>tickets</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DepartmentId);
        builder.Property(x => x.AssignedToStaffId);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Replies)
            .WithOne()
            .HasForeignKey("TicketId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Replies)
            .HasField("_replies")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
