using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The <see cref="CardChargeConsent"/> lifecycle: it starts Pending against a gateway session, is activated once
/// for the saved method, and is withdrawn from either state; a withdrawal is final, so a late activation never
/// brings it back.
/// </summary>
public sealed class CardChargeConsentTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a pending consent for client 7 at module <c>gw</c>.</summary>
    /// <returns>A new pending consent.</returns>
    private static CardChargeConsent NewConsent() => CardChargeConsent.Create(7, "gw", "sess_1", "203.0.113.5", "agent", Now);

    /// <summary>A new consent is Pending and carries the module, the session and the evidence, with no method yet.</summary>
    [Fact]
    public void Create_StartsPendingWithModuleSessionAndEvidence()
    {
        var consent = NewConsent();

        consent.Status.Should().Be(CardConsentStatus.Pending);
        consent.ClientId.Should().Be(7);
        consent.GatewayModuleId.Should().Be("gw");
        consent.GatewaySessionId.Should().Be("sess_1");
        consent.GatewayMethodRef.Should().BeNull();
        consent.IpAddress.Should().Be("203.0.113.5");
        consent.UserAgent.Should().Be("agent");
        consent.TextVersion.Should().Be(CardChargeConsent.CurrentTextVersion);
        consent.RequestedAt.Should().Be(Now);
        consent.ActivatedAt.Should().BeNull();
        consent.RevokedAt.Should().BeNull();
        consent.RevocationReason.Should().BeNull();
    }

    /// <summary>A Pending consent is activated for the method, once.</summary>
    [Fact]
    public void Activate_FromPending_BecomesActiveOnce()
    {
        var consent = NewConsent();

        consent.Activate("pm_1", Now.AddMinutes(1)).Should().BeTrue();
        consent.Activate("pm_2", Now.AddMinutes(2)).Should().BeFalse();

        consent.Status.Should().Be(CardConsentStatus.Active);
        consent.GatewayMethodRef.Should().Be("pm_1");
        consent.ActivatedAt.Should().Be(Now.AddMinutes(1));
    }

    /// <summary>A withdrawn consent is never activated again, however late the confirmation arrives.</summary>
    [Fact]
    public void Activate_AfterRevoke_IsRefusedAndStaysRevoked()
    {
        var consent = NewConsent();
        consent.Revoke(CardConsentRevocationReason.WithdrawnByClient, Now.AddMinutes(1));

        consent.Activate("pm_1", Now.AddMinutes(2)).Should().BeFalse();

        consent.Status.Should().Be(CardConsentStatus.Revoked);
        consent.GatewayMethodRef.Should().BeNull();
        consent.ActivatedAt.Should().BeNull();
    }

    /// <summary>A consent withdrawn once keeps its first reason and moment; a second withdrawal changes nothing.</summary>
    [Fact]
    public void Revoke_Twice_SecondIsRefusedAndKeepsTheFirstReason()
    {
        var consent = NewConsent();

        consent.Revoke(CardConsentRevocationReason.WithdrawnByClient, Now.AddMinutes(1)).Should().BeTrue();
        consent.Revoke(CardConsentRevocationReason.CardRemoved, Now.AddMinutes(2)).Should().BeFalse();

        consent.RevocationReason.Should().Be(CardConsentRevocationReason.WithdrawnByClient);
        consent.RevokedAt.Should().Be(Now.AddMinutes(1));
    }

    /// <summary>A consent can be withdrawn while still Pending.</summary>
    [Fact]
    public void Revoke_FromPending_Revokes()
    {
        var consent = NewConsent();

        consent.Revoke(CardConsentRevocationReason.CardRemoved, Now.AddMinutes(1)).Should().BeTrue();

        consent.Status.Should().Be(CardConsentStatus.Revoked);
        consent.RevocationReason.Should().Be(CardConsentRevocationReason.CardRemoved);
    }

    /// <summary>An Active consent can be withdrawn and keeps the method it was given for.</summary>
    [Fact]
    public void Revoke_FromActive_RevokesAndKeepsTheMethod()
    {
        var consent = NewConsent();
        consent.Activate("pm_1", Now.AddMinutes(1));

        consent.Revoke(CardConsentRevocationReason.WithdrawnByClient, Now.AddMinutes(2)).Should().BeTrue();

        consent.Status.Should().Be(CardConsentStatus.Revoked);
        consent.GatewayMethodRef.Should().Be("pm_1");
        consent.RevokedAt.Should().Be(Now.AddMinutes(2));
    }
}
