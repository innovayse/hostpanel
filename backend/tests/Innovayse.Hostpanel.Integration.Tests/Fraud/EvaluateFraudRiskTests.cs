using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Queries.EvaluateFraudRisk;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary>
/// Scoring a registration or an order against Postgres, with every local signal, the block list, the thresholds and the
/// external scoring service scripted by a fake. The use case has no route of its own: the Orders and Auth use cases ask it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EvaluateFraudRiskTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Asks the use case on a host whose caller comes from the given address.</summary>
    /// <param name="host">The host.</param>
    /// <param name="query">What to check.</param>
    /// <returns>The verdict.</returns>
    private static async Task<FraudVerdictDto> EvaluateAsync(WebApplicationFactory<Program> host, EvaluateFraudRiskQuery query)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var verdict = await bus.InvokeAsync<Result<FraudVerdictDto>>(query);
        verdict.IsSuccess.Should().BeTrue();
        return verdict.Value!;
    }

    /// <summary>Adds a block list entry through the real endpoint.</summary>
    /// <param name="type">The type name on the wire.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes once the entry is listed.</returns>
    private async Task BlockAsync(string type, string value)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type, value })).EnsureSuccessStatusCode();
    }

    /// <summary>With fraud detection off nothing is checked, nothing is scored and the request is approved.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenSwitchedOff_NothingIsChecked()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, enabled: false);
        var provider = new FakeFraudScoringProvider(new FraudProviderAssessmentDto(99, [FraudReasonCode.ProviderHighRisk], "US"));
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.10", provider);

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "x@mailinator.com", null));

        verdict.Should().Be(new FraudVerdictDto(false, FraudDecision.Approved, 0, []));
        provider.Asked.Should().BeEmpty("a switched off check never asks the external service");
    }

    /// <summary>An ordinary request from an unremarkable address is approved with no reasons.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOrdinaryRequest_IsApproved()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.11");

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "ada@example.com", "AM"));

        verdict.Should().BeEquivalentTo(new FraudVerdictDto(true, FraudDecision.Approved, 0, []));
    }

    /// <summary>A disposable e-mail address alone lands in the review band.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADisposableEmail_IsHeldForReview()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.12");

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "x@mailinator.com", null));

        verdict.Decision.Should().Be(FraudDecision.Review);
        verdict.Reasons.Should().Equal(FraudReasonCode.DisposableEmail);
    }

    /// <summary>A blocked address, range, e-mail domain or country is refused whatever else is known.</summary>
    /// <param name="type">The block list type.</param>
    /// <param name="value">The blocked value.</param>
    /// <param name="ip">The caller's address.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="country">The billing country.</param>
    /// <param name="reason">The reason the block list reports.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Ip", "192.0.2.50", "192.0.2.50", "ok@example.com", null, FraudReasonCode.BlockedIp)]
    [InlineData("Cidr", "192.0.2.0/24", "192.0.2.77", "ok@example.com", null, FraudReasonCode.BlockedIp)]
    [InlineData("EmailDomain", "blocked-domain.example", "192.0.2.51", "x@mail.blocked-domain.example", null, FraudReasonCode.BlockedEmailDomain)]
    [InlineData("Country", "KP", "192.0.2.52", "ok@example.com", "KP", FraudReasonCode.BlockedCountry)]
    public async Task ABlockListHit_IsRejected(string type, string value, string ip, string email, string? country, FraudReasonCode reason)
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        await BlockAsync(type, value);
        var host = FraudTestSeeds.WithCallerAt(factory, ip);

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Registration, email, country));

        verdict.Decision.Should().Be(FraudDecision.Rejected);
        verdict.Score.Should().Be(100);
        verdict.Reasons.Should().Contain(reason);
    }

    /// <summary>Orders already logged from the address within the window push the next one over the velocity limit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TooManyOrdersFromOneAddress_ScoreTheVelocitySignal()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, maxOrders: 2, reject: 95, review: 40);
        const string Ip = "198.51.100.20";
        for (var i = 0; i < 2; i++)
        {
            await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
                FraudCheckKind.Order, FraudDecision.Approved, null, null, Ip, $"buyer{i}@example.com", 0, []));
        }

        var verdict = await EvaluateAsync(FraudTestSeeds.WithCallerAt(factory, Ip), new EvaluateFraudRiskQuery(FraudCheckKind.Order, "next@example.com", null));

        verdict.Reasons.Should().Contain(FraudReasonCode.OrderVelocity);
        verdict.Decision.Should().Be(FraudDecision.Review);
    }

    /// <summary>A staff decision on a held order, and orders from another address, do not count as orders from this address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualDecisionsAndOtherAddresses_AreNotCounted()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, maxOrders: 1);
        const string Ip = "198.51.100.21";
        await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.ManualApproved, null, 1, Ip, "a@example.com", 60, []));
        await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Approved, null, 2, "198.51.100.22", "b@example.com", 0, []));

        var verdict = await EvaluateAsync(FraudTestSeeds.WithCallerAt(factory, Ip), new EvaluateFraudRiskQuery(FraudCheckKind.Order, "c@example.com", null));

        verdict.Reasons.Should().NotContain(FraudReasonCode.OrderVelocity);
    }

    /// <summary>Other e-mail addresses seen from the address reach the shared account limit; the same address again does not count.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeveralAccountsOnOneAddress_ScoreTheSharedIpSignal()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, maxAccounts: 3, review: 30, reject: 95);
        const string Ip = "198.51.100.30";
        foreach (var email in new[] { "one@example.com", "two@example.com", "own@example.com" })
        {
            await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
                FraudCheckKind.Registration, FraudDecision.Approved, null, null, Ip, email, 0, []));
        }

        var host = FraudTestSeeds.WithCallerAt(factory, Ip);
        var newcomer = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Registration, "new@example.com", null));
        var returning = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Registration, "OWN@example.com", null));

        newcomer.Reasons.Should().Contain(FraudReasonCode.SharedIpAccounts);
        newcomer.Decision.Should().Be(FraudDecision.Review);
        returning.Reasons.Should().NotContain(FraudReasonCode.SharedIpAccounts);
    }

    /// <summary>The external service's score and reasons are combined with the local ones: the higher score counts and the country it reports feeds the mismatch signal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheExternalServiceIsCombinedWithTheLocalScore()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var provider = new FakeFraudScoringProvider(new FraudProviderAssessmentDto(55, [FraudReasonCode.ProviderHighRisk, FraudReasonCode.AnonymousIp], "US"));
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.40", provider);

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "ada@example.com", "AM"));

        verdict.Score.Should().Be(55, "the higher of the local 30 and the service's 55");
        verdict.Decision.Should().Be(FraudDecision.Review);
        verdict.Reasons.Should().BeEquivalentTo(
            [FraudReasonCode.CountryMismatch, FraudReasonCode.ProviderHighRisk, FraudReasonCode.AnonymousIp]);
        provider.Asked.Should().ContainSingle().Which.Should().Be(new FraudProviderContext("198.51.100.40", "ada@example.com", "AM"));
    }

    /// <summary>A service that cannot answer leaves the local score to decide, and the request is not failed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenTheExternalServiceHasNoAnswer_TheLocalScoreDecides()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.41", new FakeFraudScoringProvider(null));

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "x@mailinator.com", null));

        verdict.Decision.Should().Be(FraudDecision.Review);
        verdict.Reasons.Should().Equal(FraudReasonCode.DisposableEmail);
    }

    /// <summary>A caller whose address is not known is scored on what is known, and the external service is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutAnAddress_OnlyTheEmailAndCountrySignalsApply()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var provider = new FakeFraudScoringProvider(new FraudProviderAssessmentDto(99, [], null));
        var host = FraudTestSeeds.WithCallerAt(factory, null, provider);

        var verdict = await EvaluateAsync(host, new EvaluateFraudRiskQuery(FraudCheckKind.Order, "x@mailinator.com", null));

        verdict.Reasons.Should().Equal(FraudReasonCode.DisposableEmail);
        provider.Asked.Should().BeEmpty();
    }
}
