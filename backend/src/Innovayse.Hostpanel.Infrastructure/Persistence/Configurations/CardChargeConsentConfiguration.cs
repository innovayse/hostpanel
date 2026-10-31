using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="CardChargeConsent"/>.</summary>
public sealed class CardChargeConsentConfiguration : IEntityTypeConfiguration<CardChargeConsent>
{
    /// <summary>Column width of an enum name stored as text.</summary>
    private const int EnumNameLength = 16;

    /// <summary>Column width of a gateway id (an intent or a payment method).</summary>
    private const int GatewayIdLength = 255;

    /// <summary>Column width of a consent wording version.</summary>
    private const int TextVersionLength = 64;

    /// <summary>Column width of an IP address (long enough for IPv6 with a zone).</summary>
    private const int IpAddressLength = 64;

    /// <summary>Column width of a browser's user agent string.</summary>
    private const int UserAgentLength = 512;

    /// <summary>Column width of the reason an agreement was withdrawn.</summary>
    private const int ReasonLength = 200;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CardChargeConsent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayIntentId).IsRequired().HasMaxLength(GatewayIdLength);
        builder.Property(x => x.GatewayPaymentMethodId).HasMaxLength(GatewayIdLength);
        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.TextVersion).IsRequired().HasMaxLength(TextVersionLength);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.IpAddress).HasMaxLength(IpAddressLength);
        builder.Property(x => x.UserAgent).HasMaxLength(UserAgentLength);
        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(ReasonLength);

        // One agreement per gateway intent: confirming the same intent twice finds the same row.
        builder.HasIndex(x => x.GatewayIntentId).IsUnique();

        // A saved card has at most one active agreement; withdrawn ones stay as history.
        builder.HasIndex(x => new { x.ClientId, x.GatewayPaymentMethodId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        builder.HasIndex(x => new { x.ClientId, x.Status });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}
