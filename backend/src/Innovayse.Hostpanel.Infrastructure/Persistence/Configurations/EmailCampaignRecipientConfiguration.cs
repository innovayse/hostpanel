using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="EmailCampaignRecipient"/>.</summary>
public sealed class EmailCampaignRecipientConfiguration : IEntityTypeConfiguration<EmailCampaignRecipient>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailCampaignRecipient> builder)
    {
        builder.HasKey(x => x.Id);

        // The sender and the unsubscribe path may touch one recipient at once; the second save of a changed
        // row fails with a concurrency conflict instead of overwriting the first outcome.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Email).IsRequired().HasMaxLength(EmailCampaignRecipient.EmailMaxLength);
        builder.Property(x => x.Language).HasMaxLength(EmailCampaignRecipient.LanguageMaxLength);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(EmailCampaignRecipient.FailureReasonMaxLength);
        builder.Property(x => x.UnsubscribeTokenHash).HasMaxLength(EmailCampaignRecipient.UnsubscribeTokenHashLength);

        // The public unsubscribe link names its recipient by the hash of its token alone, so the lookup is unique and indexed;
        // a recipient not yet mailed has none, and PostgreSQL lets any number of rows hold null in a unique index.
        builder.HasIndex(x => x.UnsubscribeTokenHash).IsUnique();

        // One client receives a campaign once.
        builder.HasIndex(x => new { x.CampaignId, x.ClientId }).IsUnique();

        // The sender takes the next pending recipients of one campaign in id order; the index serves that query without a sort.
        builder.HasIndex(x => new { x.CampaignId, x.Status, x.Id });

        // A recipient has no meaning without its client; deleting the client removes the row.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
