using System.Net;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Vat;
using Innovayse.Hostpanel.Infrastructure.Vat.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Vat;

/// <summary>
/// The UK VAT number lookup adapter against a loopback server that replays answers shaped like the authority's: a token,
/// then valid, invalid, a service error and no answer in time. A refusal that is about the request rather than the number is
/// never an invalid number.
/// </summary>
public sealed class HmrcVatClientTests : IAsyncLifetime
{
    /// <summary>The token the stub authority hands out.</summary>
    private const string Token = """{"access_token":"server-token-1","token_type":"bearer","expires_in":14400,"scope":"read:vat"}""";

    /// <summary>The loopback server standing in for the authority; started per test class instance.</summary>
    private StubHttpServer http = null!;

    /// <summary>Starts the stub server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        http = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await http.DisposeAsync();
    }

    /// <summary>Builds the adapter over a client aimed at the stub server.</summary>
    /// <param name="configured">Whether the application's credentials are set.</param>
    /// <param name="timeout">The client's own timeout, or null for the default.</param>
    /// <returns>The adapter under test.</returns>
    private HmrcVatClient Client(bool configured = true, TimeSpan? timeout = null) =>
        new(
            new HttpClient { BaseAddress = http.BaseAddress, Timeout = timeout ?? TimeSpan.FromSeconds(30) },
            Options.Create(configured
                ? new HmrcVatOptions { ClientId = "app-id", ClientSecret = "app-secret" }
                : new HmrcVatOptions()),
            NullLogger<HmrcVatClient>.Instance);

    /// <summary>Asks for a UK number.</summary>
    /// <param name="client">The adapter.</param>
    /// <returns>The adapter's answer.</returns>
    private static Task<VatNumberCheck> CheckAsync(HmrcVatClient client) =>
        client.CheckAsync(new VatNumberLookup("GB", "123456789"), CancellationToken.None);

    /// <summary>A registered number is valid, carries the name and address, and is asked for with the token as a bearer.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_RegisteredNumber_IsValidWithTheDisclosedDetails()
    {
        http.Enqueue(Token);
        http.Enqueue("""
            {"target":{"name":"Acme Ltd","vatNumber":"123456789",
             "address":{"line1":"1 High Street","line2":"Floor 2","postcode":"SW1A 1AA","countryCode":"GB"}},
             "processingDate":"2026-11-10T09:00:00+00:00"}
            """);

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Valid);
        answer.Name.Should().Be("Acme Ltd");
        answer.Address.Should().Be("1 High Street, Floor 2, SW1A 1AA, GB");
        http.Requests.Should().HaveCount(2);
        http.Requests[0].PathAndQuery.Should().EndWith(HmrcVatClient.TokenPath);
        http.Requests[0].Body.Should().Contain("client_id=app-id").And.Contain("grant_type=client_credentials");
        http.Requests[1].Method.Should().Be("GET");
        http.Requests[1].PathAndQuery.Should().EndWith(HmrcVatClient.LookupPath + "123456789");
        http.Requests[1].Authorization.Should().Be("Bearer server-token-1");
    }

    /// <summary>A number that is not registered is invalid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_UnregisteredNumber_IsInvalid()
    {
        http.Enqueue(Token);
        http.Enqueue(HttpStatusCode.NotFound, """{"code":"NOT_FOUND","message":"targetVrn does not match a registered company"}""");

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Invalid);
    }

    /// <summary>A number that is not shaped like one is invalid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_MalformedNumber_IsInvalid()
    {
        http.Enqueue(Token);
        http.Enqueue(HttpStatusCode.BadRequest, """{"code":"INVALID_REQUEST","message":"Invalid targetVrn"}""");

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Invalid);
    }

    /// <summary>
    /// A refusal that is about the request, not the number (a bad header, a rejected token, the authority throttling or down)
    /// is unavailable and never invalid.
    /// </summary>
    /// <param name="status">The status the authority answered with.</param>
    /// <param name="body">The error body.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"code":"ACCEPT_HEADER_INVALID"}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"code":"INVALID_CREDENTIALS"}""")]
    [InlineData(HttpStatusCode.Forbidden, """{"code":"FORBIDDEN"}""")]
    [InlineData(HttpStatusCode.NotFound, """{"code":"MATCHING_RESOURCE_NOT_FOUND"}""")]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":"MESSAGE_THROTTLED_OUT"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"code":"SERVER_ERROR"}""")]
    public async Task CheckAsync_RefusalAboutTheRequest_IsUnavailableNotInvalid(HttpStatusCode status, string body)
    {
        http.Enqueue(Token);
        http.Enqueue(status, body);

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>A token request the authority refuses is unavailable, and no lookup is attempted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_TokenRefused_IsUnavailableAndAsksNothingElse()
    {
        http.Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""");

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
        http.Requests.Should().ContainSingle();
    }

    /// <summary>Without the application's credentials nothing is asked and the answer is unavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_WithoutCredentials_IsUnavailableAndCallsNothing()
    {
        var answer = await CheckAsync(Client(configured: false));

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
        http.Requests.Should().BeEmpty();
    }

    /// <summary>The adapter reports whether the application's credentials are set, so a caller can tell a missing set-up from a busy service.</summary>
    [Fact]
    public void IsConfigured_FollowsTheCredentials()
    {
        Client(configured: true).IsConfigured.Should().BeTrue();
        Client(configured: false).IsConfigured.Should().BeFalse();
    }

    /// <summary>A body that is not the published object is unavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_UnreadableBody_IsUnavailable()
    {
        http.Enqueue(Token);
        http.Enqueue("<html>maintenance</html>");

        var answer = await CheckAsync(Client());

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>An authority that does not answer in time is unavailable, and the call returns instead of waiting for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_Timeout_IsUnavailable()
    {
        http.Enqueue(new StubReply(HttpStatusCode.OK, Token, Delay: TimeSpan.FromSeconds(30)));

        var answer = await CheckAsync(Client(timeout: TimeSpan.FromMilliseconds(300))).WaitAsync(TimeSpan.FromSeconds(20));

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>The credentials are all or nothing: a client id without its secret refuses startup, an empty section does not.</summary>
    [Fact]
    public void Options_PartlyFilledCredentials_AreNotUsable()
    {
        new HmrcVatOptions().IsUsable.Should().BeTrue();
        new HmrcVatOptions { ClientId = "a", ClientSecret = "b" }.IsUsable.Should().BeTrue();
        new HmrcVatOptions { ClientId = "a" }.IsUsable.Should().BeFalse();
        new HmrcVatOptions { ClientSecret = "b" }.IsUsable.Should().BeFalse();
    }
}
