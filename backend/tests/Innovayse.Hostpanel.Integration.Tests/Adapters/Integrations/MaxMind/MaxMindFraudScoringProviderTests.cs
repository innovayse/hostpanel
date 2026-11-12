using System.Net;
using System.Net.Http.Json;
using System.Text;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.MaxMind;

/// <summary>
/// Tests for <see cref="MaxMindFraudScoringProvider"/> over a scripted remote service: it is silent until the integration is set up and
/// switched on, sends only what it needs, reads the answer into the host's assessment, and ends every failure as no answer.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host; its database holds the integration's stored settings.</param>
public sealed class MaxMindFraudScoringProviderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The request every test asks about.</summary>
    private static readonly FraudProviderContext Asked = new("198.51.100.70", "Ada@Example.com", "AM");

    /// <summary>A realistic Insights answer for a risky request behind a VPN in another country.</summary>
    private const string RiskyAnswer =
        "{\"risk_score\":72.5,\"ip_address\":{\"country\":{\"iso_code\":\"us\"},\"traits\":{\"is_anonymous\":true,\"is_anonymous_vpn\":true}}}";

    /// <summary>Saves the integration's settings through the real admin endpoint; a field given as <see langword="null"/> is cleared, so what an earlier test stored in the shared database does not leak in.</summary>
    /// <param name="enabled">Whether the integration is switched on.</param>
    /// <param name="accountId">The account id, or <see langword="null"/> for none.</param>
    /// <param name="licenseKey">The license key, or <see langword="null"/> for none.</param>
    /// <returns>A task that completes once the settings are saved.</returns>
    private async Task ConfigureAsync(bool enabled, string? accountId = "12345", string? licenseKey = "license-secret")
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var config = new Dictionary<string, string>();
        var clearFields = new List<string>();
        if (accountId is not null)
        {
            config["account_id"] = accountId;
        }
        else
        {
            clearFields.Add("account_id");
        }

        if (licenseKey is not null)
        {
            config["license_key"] = licenseKey;
        }
        else
        {
            clearFields.Add("license_key");
        }

        var response = await admin.PutAsJsonAsync("/api/admin/integrations/maxmind", new { isEnabled = enabled, config, clearFields });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Asks the provider through a handler that plays the remote service.</summary>
    /// <param name="handler">The scripted remote service.</param>
    /// <param name="ct">The caller's cancellation token.</param>
    /// <returns>The provider's answer.</returns>
    private async Task<FraudProviderAssessmentDto?> AskAsync(ScriptedHttpMessageHandler handler, CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var http = new HttpClient(handler) { BaseAddress = new Uri(MaxMindFraudScoringProvider.BaseAddress) };
        var provider = new MaxMindFraudScoringProvider(
            http,
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>(),
            NullLogger<MaxMindFraudScoringProvider>.Instance);

        return await provider.ScoreAsync(Asked, ct);
    }

    /// <summary>Before the integration is set up the service is never called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NotSetUp_NeverCallsTheService()
    {
        await ConfigureAsync(enabled: false, accountId: null, licenseKey: null);
        var handler = ScriptedHttpMessageHandler.Json(RiskyAnswer);

        (await AskAsync(handler)).Should().BeNull();

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>Credentials that are stored but switched off are not used.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SwitchedOff_NeverCallsTheService()
    {
        await ConfigureAsync(enabled: false);
        var handler = ScriptedHttpMessageHandler.Json(RiskyAnswer);

        (await AskAsync(handler)).Should().BeNull();

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A half-filled integration is not used.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutALicenseKey_NeverCallsTheService()
    {
        await ConfigureAsync(enabled: true, licenseKey: null);
        var handler = ScriptedHttpMessageHandler.Json(RiskyAnswer);

        (await AskAsync(handler)).Should().BeNull();

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A set up integration signs the call with the account id and license key, and sends the address, the e-mail domain and the country only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SendsOnlyWhatItNeeds_SignedWithTheStoredCredentials()
    {
        await ConfigureAsync(enabled: true);
        HttpMethod? method = null;
        System.Net.Http.Headers.AuthenticationHeaderValue? authorization = null;
        var handler = new ScriptedHttpMessageHandler((request, _) =>
        {
            method = request.Method;
            authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RiskyAnswer, Encoding.UTF8, "application/json") });
        });

        await AskAsync(handler);

        handler.Requests.Should().ContainSingle().Which.AbsoluteUri.Should().Be("https://minfraud.maxmind.com/minfraud/v2.0/insights");
        method.Should().Be(HttpMethod.Post);
        authorization!.Scheme.Should().Be("Basic");
        authorization.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("12345:license-secret")));
        var body = handler.Bodies.Should().ContainSingle().Subject;
        body.Should().Contain("\"ip_address\":\"198.51.100.70\"").And.Contain("\"domain\":\"Example.com\"").And.Contain("\"country\":\"AM\"");
        body.Should().NotContain("Ada@", "the e-mail address itself never leaves the host");
    }

    /// <summary>The answer is read into a score, the reasons it names and the country of the address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReadsTheAnswerIntoAnAssessment()
    {
        await ConfigureAsync(enabled: true);

        var assessment = await AskAsync(ScriptedHttpMessageHandler.Json(RiskyAnswer));

        assessment.Should().NotBeNull();
        assessment!.Score.Should().Be(73);
        assessment.IpCountry.Should().Be("US");
        assessment.Reasons.Should().Equal(FraudReasonCode.ProviderHighRisk, FraudReasonCode.AnonymousIp);
    }

    /// <summary>A low score from an ordinary address names no reason, and an answer without a score reads as no risk.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALowScore_NamesNoReason()
    {
        await ConfigureAsync(enabled: true);

        var low = await AskAsync(ScriptedHttpMessageHandler.Json("{\"risk_score\":3.1,\"ip_address\":{\"country\":{\"iso_code\":\"AM\"}}}"));
        var empty = await AskAsync(ScriptedHttpMessageHandler.Json("{}"));

        low.Should().BeEquivalentTo(new FraudProviderAssessmentDto(3, [], "AM"));
        empty.Should().BeEquivalentTo(new FraudProviderAssessmentDto(0, [], null));
    }

    /// <summary>Every way the call fails ends as no answer: a refusal, an unreachable service, a timeout and a body that is not JSON.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryFailure_EndsAsNoAnswer()
    {
        await ConfigureAsync(enabled: true);

        (await AskAsync(ScriptedHttpMessageHandler.Json("{\"code\":\"AUTHORIZATION_INVALID\"}", HttpStatusCode.Unauthorized))).Should().BeNull();
        (await AskAsync(ScriptedHttpMessageHandler.Json("oops", HttpStatusCode.InternalServerError))).Should().BeNull();
        (await AskAsync(new ScriptedHttpMessageHandler((_, _) => throw new HttpRequestException("unreachable")))).Should().BeNull();
        (await AskAsync(new ScriptedHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out")))).Should().BeNull();
        (await AskAsync(ScriptedHttpMessageHandler.Json("<html>not json</html>"))).Should().BeNull();
    }

    /// <summary>A caller that cancels is not told "no answer": the cancellation goes through.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACancelledCaller_IsNotSwallowed()
    {
        await ConfigureAsync(enabled: true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => AskAsync(new ScriptedHttpMessageHandler((_, _) => throw new TaskCanceledException()), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
