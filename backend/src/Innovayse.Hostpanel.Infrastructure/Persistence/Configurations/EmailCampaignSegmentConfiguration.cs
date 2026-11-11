using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="EmailCampaignSegment"/>.</summary>
public sealed class EmailCampaignSegmentConfiguration : IEntityTypeConfiguration<EmailCampaignSegment>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailCampaignSegment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.FilterType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.FilterValue).HasMaxLength(EmailCampaignSegment.FilterValueMaxLength);
        builder.HasIndex(x => x.CampaignId);
    }
}
