using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The <see cref="InvoicePaymentClaim"/> lifecycle: a claim carries the module and the gateway key, goes stale
/// only when unfinished and past its lifetime, ends exactly once, and builds a gateway key that fits the gateways'
/// length limit for any invoice id.
/// </summary>
public sealed class InvoicePaymentClaimTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The claim lifetime every test claim is taken with.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Builds an unfinished manual claim on invoice 5 at module <c>gw</c>.</summary>
    /// <returns>A new claim started at <see cref="Now"/>.</returns>
    private static InvoicePaymentClaim NewClaim() => InvoicePaymentClaim.Create(5, "gw", InvoicePaymentClaimKind.Manual, "key-1", Now, Lifetime);

    /// <summary>A new claim carries its invoice, module, kind, key and window, and has no outcome.</summary>
    [Fact]
    public void Create_SetsTheFieldsAndIsUnfinished()
    {
        var claim = NewClaim();

        claim.InvoiceId.Should().Be(5);
        claim.GatewayModuleId.Should().Be("gw");
        claim.Kind.Should().Be(InvoicePaymentClaimKind.Manual);
        claim.GatewayIdempotencyKey.Should().Be("key-1");
        claim.StartedAt.Should().Be(Now);
        claim.ExpiresAt.Should().Be(Now + Lifetime);
        claim.Outcome.Should().BeNull();
        claim.CompletedAt.Should().BeNull();
    }

    /// <summary>The automatic key is built from the invoice and the token and fits the 64 characters gateways accept.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(123456)]
    [InlineData(int.MaxValue)]
    public void AutomaticKey_HasTheDocumentedShapeAndFitsTheGatewayLimit(int invoiceId)
    {
        var token = Guid.NewGuid();

        var key = InvoicePaymentClaim.AutomaticKey(invoiceId, token);

        key.Should().Be($"invoice-{invoiceId}-auto-{token:N}");
        key.Length.Should().BeLessThanOrEqualTo(64);
    }

    /// <summary>A claim is stale only once its lifetime has passed.</summary>
    [Fact]
    public void IsStale_BeforeAndAfterExpiry()
    {
        var claim = NewClaim();

        claim.IsStale(Now + Lifetime - TimeSpan.FromSeconds(1)).Should().BeFalse();
        claim.IsStale(Now + Lifetime).Should().BeTrue();
        claim.IsStale(Now + Lifetime + TimeSpan.FromHours(1)).Should().BeTrue();
    }

    /// <summary>A finished claim is never stale: there is nothing left to look up.</summary>
    [Fact]
    public void IsStale_WhenEnded_IsFalse()
    {
        var claim = NewClaim();
        claim.MarkFailed(Now.AddMinutes(1));

        claim.IsStale(Now + Lifetime + TimeSpan.FromHours(1)).Should().BeFalse();
    }

    /// <summary>A claim ends with the outcome that was set first and the first moment; later endings change nothing.</summary>
    [Fact]
    public void End_HappensOnce_LaterMarksChangeNothing()
    {
        var claim = NewClaim();

        claim.MarkSettled(Now.AddMinutes(1));
        claim.MarkFailed(Now.AddMinutes(2));
        claim.MarkExpired(Now.AddMinutes(3));

        claim.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
        claim.CompletedAt.Should().Be(Now.AddMinutes(1));
    }

    /// <summary>Each ending records its own outcome.</summary>
    [Theory]
    [InlineData(InvoicePaymentClaimOutcome.Settled)]
    [InlineData(InvoicePaymentClaimOutcome.Failed)]
    [InlineData(InvoicePaymentClaimOutcome.Expired)]
    public void Mark_RecordsTheOutcome(InvoicePaymentClaimOutcome outcome)
    {
        var claim = NewClaim();

        switch (outcome)
        {
            case InvoicePaymentClaimOutcome.Settled:
                claim.MarkSettled(Now);
                break;
            case InvoicePaymentClaimOutcome.Failed:
                claim.MarkFailed(Now);
                break;
            default:
                claim.MarkExpired(Now);
                break;
        }

        claim.Outcome.Should().Be(outcome);
        claim.CompletedAt.Should().Be(Now);
    }
}
