using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="TrialReminder"/> (table <c>TrialReminders</c>).</summary>
public sealed class TrialReminderConfiguration : IEntityTypeConfiguration<TrialReminder>
{
    /// <summary>The name of the unique index on the service and the reminder day, so a reminder is recorded once.</summary>
    public const string ServiceDaysBeforeIndex = "IX_TrialReminders_ServiceId_DaysBefore";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TrialReminder> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.Property(x => x.DaysBefore).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();

        // A reminder has no meaning without its service.
        builder.HasOne<ClientService>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One reminder per service and reminder day; the database refuses a second one.
        builder.HasIndex(x => new { x.ServiceId, x.DaysBefore })
            .IsUnique()
            .HasDatabaseName(ServiceDaysBeforeIndex);
    }
}
