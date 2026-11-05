using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="CardChargeConsent"/> aggregate.</summary>
public sealed class CardChargeConsentConfiguration : IEntityTypeConfiguration<CardChargeConsent>
{
    /// <summary>Configures the <c>CardChargeConsents</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<CardChargeConsent> builder)
    {
        builder.HasKey(x => x.Id);

        // Two writers revoking or completing the same consent cannot both win silently.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.GatewayModuleId).IsRequired().HasMaxLength(GatewayCustomer.MaxLength);
        builder.Property(x => x.SessionId).IsRequired().HasMaxLength(InvoicePaymentClaim.MaxLength);
        builder.Property(x => x.MethodRef).HasMaxLength(InvoicePaymentClaim.MaxLength);
        builder.Property(x => x.ConsentTextVersion).HasMaxLength(CardChargeConsent.MaxTextVersionLength);
        builder.Property(x => x.IpAddress).HasMaxLength(CardChargeConsent.MaxIpAddressLength);
        builder.Property(x => x.UserAgent).HasMaxLength(CardChargeConsent.MaxUserAgentLength);
        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.GrantedAt).IsRequired();
        builder.Property(x => x.RevokedAt);

        // The ignored computed members are read-only views of the columns above.
        builder.Ignore(x => x.IsRevoked);
        builder.Ignore(x => x.IsActive);

        // A save session is completed by finding its consent again; one session is one consent.
        builder.HasIndex(x => new { x.GatewayModuleId, x.SessionId }).IsUnique();

        // The charge path asks for a client's consents under a module.
        builder.HasIndex(x => new { x.ClientId, x.GatewayModuleId });

        // A client who has recorded a consent is kept: the consent is the proof the charges were agreed to.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
