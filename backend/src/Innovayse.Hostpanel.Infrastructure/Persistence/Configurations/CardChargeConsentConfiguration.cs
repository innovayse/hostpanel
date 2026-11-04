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

    /// <summary>Column width of a revocation reason name; <c>WithdrawnByClient</c> alone is 17 characters.</summary>
    private const int RevocationReasonLength = 32;

    /// <summary>Column width of a gateway id (a save session or a saved method).</summary>
    private const int GatewayIdLength = 255;

    /// <summary>Column width of a payment gateway module id.</summary>
    private const int ModuleIdLength = 100;

    /// <summary>Column width of a consent wording version.</summary>
    private const int TextVersionLength = 64;

    /// <summary>Column width of an IP address (long enough for IPv6 with a zone).</summary>
    private const int IpAddressLength = 64;

    /// <summary>Column width of a browser's user agent string.</summary>
    private const int UserAgentLength = 512;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CardChargeConsent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayModuleId).IsRequired().HasMaxLength(ModuleIdLength);
        builder.Property(x => x.GatewaySessionId).IsRequired().HasMaxLength(GatewayIdLength);
        builder.Property(x => x.GatewayMethodRef).HasMaxLength(GatewayIdLength);
        builder.Property(x => x.TextVersion).IsRequired().HasMaxLength(TextVersionLength);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.IpAddress).HasMaxLength(IpAddressLength);
        builder.Property(x => x.UserAgent).HasMaxLength(UserAgentLength);
        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(RevocationReasonLength);

        // A withdrawal and an activation both rewrite the row. The token makes the save of whichever read the
        // older state fail instead of silently overwriting the other, so a withdrawal cannot be lost.
        builder.UseXminConcurrencyToken();

        // One agreement per save session: confirming the same session twice finds the same row.
        builder.HasIndex(x => new { x.GatewayModuleId, x.GatewaySessionId }).IsUnique();

        // A saved card has at most one active agreement; withdrawn ones stay as history.
        builder.HasIndex(x => new { x.ClientId, x.GatewayModuleId, x.GatewayMethodRef })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        builder.HasIndex(x => new { x.ClientId, x.Status });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}
