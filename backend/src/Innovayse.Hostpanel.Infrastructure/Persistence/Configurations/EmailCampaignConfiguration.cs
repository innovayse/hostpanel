using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="EmailCampaign"/>.</summary>
public sealed class EmailCampaignConfiguration : IEntityTypeConfiguration<EmailCampaign>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailCampaign> builder)
    {
        builder.HasKey(x => x.Id);

        // Two admins editing, or an edit racing the scheduler's start, cannot both win: the second save of a
        // changed row fails with a concurrency conflict (409) instead of silently overwriting the first.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.Subject).IsRequired().HasMaxLength(EmailCampaign.SubjectMaxLength);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(EmailCampaign.BodyMaxLength);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(EmailCampaign.CreatedByMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        // The scheduler asks for the scheduled campaigns that are due.
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });

        builder.HasMany(x => x.Segments)
            .WithOne()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Segments).AutoInclude();

        builder.HasMany(x => x.Recipients)
            .WithOne()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
