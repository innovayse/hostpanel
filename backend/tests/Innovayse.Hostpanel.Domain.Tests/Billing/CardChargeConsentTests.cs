using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The <see cref="CardChargeConsent"/> lifecycle: recorded before the method exists, bound once the session completes,
/// active only with a method and no revocation, and final once revoked.
/// </summary>
public sealed class CardChargeConsentTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a consent for client 3 under a save session.</summary>
    /// <returns>A new consent with no method.</returns>
    private static CardChargeConsent NewConsent()
    {
        return CardChargeConsent.Grant(3, "bank-gateway", "session-1", Now);
    }

    /// <summary>A consent recorded before the gateway step covers no method yet, so it is not active.</summary>
    [Fact]
    public void Grant_HasNoMethodAndIsNotActive()
    {
        var consent = NewConsent();

        consent.ClientId.Should().Be(3);
        consent.GatewayModuleId.Should().Be("bank-gateway");
        consent.SessionId.Should().Be("session-1");
        consent.MethodRef.Should().BeNull();
        consent.GrantedAt.Should().Be(Now);
        consent.IsActive.Should().BeFalse();
        consent.IsRevoked.Should().BeFalse();
    }

    /// <summary>Attaching the method makes the consent active.</summary>
    [Fact]
    public void AttachMethod_MakesTheConsentActive()
    {
        var consent = NewConsent();

        consent.AttachMethod("pm-1");

        consent.MethodRef.Should().Be("pm-1");
        consent.IsActive.Should().BeTrue();
    }

    /// <summary>Attaching the same method again changes nothing, because completing a session is idempotent.</summary>
    [Fact]
    public void AttachMethod_SameMethodAgain_IsANoOp()
    {
        var consent = NewConsent();
        consent.AttachMethod("pm-1");

        var act = () => consent.AttachMethod("pm-1");

        act.Should().NotThrow();
        consent.MethodRef.Should().Be("pm-1");
    }

    /// <summary>A consent never covers a second, different method.</summary>
    [Fact]
    public void AttachMethod_DifferentMethod_Throws()
    {
        var consent = NewConsent();
        consent.AttachMethod("pm-1");

        var act = () => consent.AttachMethod("pm-2");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Revoking ends the consent for good: it is no longer active and takes no method.</summary>
    [Fact]
    public void Revoke_IsFinal()
    {
        var consent = NewConsent();
        consent.AttachMethod("pm-1");

        consent.Revoke(Now.AddDays(1));

        consent.IsRevoked.Should().BeTrue();
        consent.IsActive.Should().BeFalse();
        consent.RevokedAt.Should().Be(Now.AddDays(1));
        var act = () => consent.AttachMethod("pm-1");
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Revoking again keeps the first moment.</summary>
    [Fact]
    public void Revoke_Twice_KeepsTheFirstMoment()
    {
        var consent = NewConsent();
        consent.Revoke(Now.AddDays(1));

        consent.Revoke(Now.AddDays(5));

        consent.RevokedAt.Should().Be(Now.AddDays(1));
    }

    /// <summary>A consent needs a module and a session.</summary>
    [Fact]
    public void Grant_WithoutModuleOrSession_Throws()
    {
        var noModule = () => CardChargeConsent.Grant(3, " ", "session-1", Now);
        var noSession = () => CardChargeConsent.Grant(3, "bank-gateway", "", Now);

        noModule.Should().Throw<ArgumentException>();
        noSession.Should().Throw<ArgumentException>();
    }

    /// <summary>The evidence given with the consent is kept, and a browser text longer than the column is cut.</summary>
    [Fact]
    public void Grant_WithEvidence_KeepsIt()
    {
        var longAgent = new string('a', CardChargeConsent.MaxUserAgentLength + 20);

        var consent = CardChargeConsent.Grant(3, "bank-gateway", "session-1", Now, "2026-11", "203.0.113.7", longAgent);

        consent.ConsentTextVersion.Should().Be("2026-11");
        consent.IpAddress.Should().Be("203.0.113.7");
        consent.UserAgent.Should().HaveLength(CardChargeConsent.MaxUserAgentLength);
    }

    /// <summary>A consent without evidence (recorded before it was kept) has none of it.</summary>
    [Fact]
    public void Grant_WithoutEvidence_LeavesItNull()
    {
        var consent = NewConsent();

        consent.ConsentTextVersion.Should().BeNull();
        consent.IpAddress.Should().BeNull();
        consent.UserAgent.Should().BeNull();
        consent.IsDefault.Should().BeFalse();
    }

    /// <summary>A wording version longer than the column is refused.</summary>
    [Fact]
    public void Grant_WithATooLongTextVersion_Throws()
    {
        var act = () => CardChargeConsent.Grant(3, "bank-gateway", "session-1", Now, new string('v', CardChargeConsent.MaxTextVersionLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Making a consent the default clears the flag on the client's other consents under the same module only.</summary>
    [Fact]
    public void SetDefault_ClearsTheOtherConsentsOfTheClientAndModule()
    {
        var first = NewConsent();
        first.AttachMethod("pm-1");
        var second = CardChargeConsent.Grant(3, "bank-gateway", "session-2", Now);
        second.AttachMethod("pm-2");
        var otherModule = CardChargeConsent.Grant(3, "other-gateway", "session-3", Now);
        otherModule.AttachMethod("pm-3");
        first.SetDefault([second, otherModule]);
        otherModule.SetDefault([first]);

        second.SetDefault([first, second, otherModule]);

        second.IsDefault.Should().BeTrue();
        first.IsDefault.Should().BeFalse();
        otherModule.IsDefault.Should().BeTrue("a consent under another module keeps its own default");
    }

    /// <summary>A consent without a method, or a revoked one, cannot be the default.</summary>
    [Fact]
    public void SetDefault_OnAnInactiveConsent_Throws()
    {
        var noMethod = NewConsent();
        var revoked = CardChargeConsent.Grant(3, "bank-gateway", "session-2", Now);
        revoked.AttachMethod("pm-2");
        revoked.Revoke(Now);

        var withoutMethod = () => noMethod.SetDefault([]);
        var afterRevoke = () => revoked.SetDefault([]);

        withoutMethod.Should().Throw<InvalidOperationException>();
        afterRevoke.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Revoking the default consent takes the default flag away with it.</summary>
    [Fact]
    public void Revoke_OnTheDefault_ClearsTheDefault()
    {
        var consent = NewConsent();
        consent.AttachMethod("pm-1");
        consent.SetDefault([]);

        consent.Revoke(Now.AddDays(1));

        consent.IsDefault.Should().BeFalse();
    }

    /// <summary>A consent accepts its own method again or a first method, and refuses a different one.</summary>
    [Fact]
    public void CanAttach_AnswersForNoMethodSameMethodAndDifferentMethod()
    {
        var consent = NewConsent();
        consent.CanAttach("pm-1").Should().BeTrue("a consent without a method takes one");
        consent.AttachMethod("pm-1");

        consent.CanAttach("pm-1").Should().BeTrue();
        consent.CanAttach("pm-2").Should().BeFalse();
    }
}
