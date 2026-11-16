using System.Net;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Vat;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Vat;

/// <summary>
/// The EU VAT number service adapter against a loopback server that replays answers shaped like the service's: valid,
/// invalid, a member state that is down, an error status, a body that is not the published object and no answer in time.
/// An answer that is not a verdict is never an invalid number.
/// </summary>
public sealed class ViesVatClientTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the service; started per test class instance.</summary>
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
    /// <param name="timeout">The client's own timeout, or null for the default.</param>
    /// <returns>The adapter under test.</returns>
    private ViesVatClient Client(TimeSpan? timeout = null) =>
        new(
            new HttpClient { BaseAddress = http.BaseAddress, Timeout = timeout ?? TimeSpan.FromSeconds(30) },
            NullLogger<ViesVatClient>.Instance);

    /// <summary>A registered number is valid and carries the name and address the service returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_RegisteredNumber_IsValidWithTheDisclosedDetails()
    {
        http.Enqueue("""
            {"countryCode":"DE","vatNumber":"123456789","requestDate":"2026-11-10T09:00:00.000Z","valid":true,
             "requestIdentifier":"","name":"Acme GmbH","address":"Main Street 1\n10115 Berlin","traderName":"---","userError":"VALID"}
            """);

        var answer = await Client().CheckAsync(new VatNumberLookup("DE", "123456789"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Valid);
        answer.Name.Should().Be("Acme GmbH");
        answer.Address.Should().Contain("Berlin");
        var call = http.Requests.Should().ContainSingle().Subject;
        call.Method.Should().Be("POST");
        call.PathAndQuery.Should().EndWith(ViesVatClient.CheckPath);
        call.Body.Should().Contain("\"countryCode\":\"DE\"").And.Contain("\"vatNumber\":\"123456789\"");
    }

    /// <summary>A member state that does not disclose the trader sends dashes, which are read as no name and no address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_UndisclosedDetails_AreNone()
    {
        http.Enqueue("""{"countryCode":"ES","vatNumber":"A12345678","valid":true,"name":"---","address":"---","userError":"VALID"}""");

        var answer = await Client().CheckAsync(new VatNumberLookup("ES", "A12345678"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Valid);
        answer.Name.Should().BeNull();
        answer.Address.Should().BeNull();
    }

    /// <summary>Greece is asked for with the prefix the service spells it with, not its ISO code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_Greece_IsAskedForAsEl()
    {
        http.Enqueue("""{"valid":true,"name":"X","address":"Y","userError":"VALID"}""");

        await Client().CheckAsync(new VatNumberLookup("GR", "123456789"), CancellationToken.None);

        http.Requests.Single().Body.Should().Contain("\"countryCode\":\"EL\"");
    }

    /// <summary>A number the register does not hold is invalid, and nothing is kept from the answer.</summary>
    /// <param name="verdict">The service's verdict code for an unregistered or malformed number.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("INVALID")]
    [InlineData("INVALID_INPUT")]
    public async Task CheckAsync_UnregisteredNumber_IsInvalid(string verdict)
    {
        http.Enqueue($$"""{"countryCode":"DE","vatNumber":"1","valid":false,"name":"---","address":"---","userError":"{{verdict}}"}""");

        var answer = await Client().CheckAsync(new VatNumberLookup("DE", "1"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Invalid);
        answer.Name.Should().BeNull();
    }

    /// <summary>
    /// A member state that is down or busy answers with a verdict code of its own and <c>valid: false</c>; that is a service
    /// error, so the answer is unavailable and never invalid.
    /// </summary>
    /// <param name="verdict">A verdict code that says the service could not judge.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("MS_UNAVAILABLE")]
    [InlineData("MS_MAX_CONCURRENT_REQ")]
    [InlineData("GLOBAL_MAX_CONCURRENT_REQ")]
    [InlineData("SERVICE_UNAVAILABLE")]
    [InlineData("TIMEOUT")]
    public async Task CheckAsync_MemberStateDown_IsUnavailableNotInvalid(string verdict)
    {
        http.Enqueue($$"""{"countryCode":"DE","vatNumber":"123456789","valid":false,"userError":"{{verdict}}"}""");

        var answer = await Client().CheckAsync(new VatNumberLookup("DE", "123456789"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>An error status from the service is unavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_ServiceError_IsUnavailable()
    {
        http.Enqueue(HttpStatusCode.InternalServerError, """{"actionSucceed":false,"errorWrappers":[{"error":"SERVICE_UNAVAILABLE"}]}""");

        var answer = await Client().CheckAsync(new VatNumberLookup("DE", "123456789"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>A body that is not the published object is unavailable too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_UnreadableBody_IsUnavailable()
    {
        http.Enqueue("<html>maintenance</html>");

        var answer = await Client().CheckAsync(new VatNumberLookup("DE", "123456789"), CancellationToken.None);

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }

    /// <summary>A service that does not answer in time is unavailable, and the call returns instead of waiting for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAsync_Timeout_IsUnavailable()
    {
        http.Enqueue(new StubReply(HttpStatusCode.OK, """{"valid":true,"userError":"VALID"}""", Delay: TimeSpan.FromSeconds(30)));

        var answer = await Client(TimeSpan.FromMilliseconds(300))
            .CheckAsync(new VatNumberLookup("DE", "123456789"), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(20));

        answer.Outcome.Should().Be(VatCheckOutcome.Unavailable);
    }
}
