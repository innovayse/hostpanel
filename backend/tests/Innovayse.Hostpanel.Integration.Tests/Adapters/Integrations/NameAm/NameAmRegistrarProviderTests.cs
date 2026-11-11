using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.NameAm;

/// <summary>
/// <see cref="NameAmRegistrarProvider"/> against a stub Name.am API: an unconfigured integration
/// and an error body are both refusals, never a success that bought nothing; a purchase is only
/// proven by a purchase list; a retry whose name the account already holds counts as placed; and
/// a change that fails is a failed result carrying a code, never an exception.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host; only its data context is used, which
/// <see cref="NameAmClient"/> reads integration overrides from (none are saved here).</param>
public sealed class NameAmRegistrarProviderTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>The loopback server standing in for the Name.am API.</summary>
    private StubHttpServer _nameAm = null!;

    /// <summary>The scope the provider's data context comes from; disposed with the test.</summary>
    private IServiceScope _scope = null!;

    /// <summary>The process-wide token holder the test's clients share, as they do in the running API.</summary>
    private readonly NameAmTokenStore _tokens = new();

    /// <summary>A contact with every detail a purchase needs.</summary>
    private static readonly DomainContact CompleteContact = new(
        "Ani", "Hakobyan", null, "ani@example.test", "+374.10123456",
        "1 Main St", null, "Yerevan", "Yerevan", "0010", "AM");

    /// <summary>Starts the stub Name.am server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        _nameAm = await StubHttpServer.StartAsync();
        _scope = factory.Services.CreateScope();
    }

    /// <summary>Stops the stub Name.am server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _nameAm.DisposeAsync();
    }

    /// <summary>Builds a provider aimed at the stub server.</summary>
    /// <param name="configured">Whether credentials are set.</param>
    /// <param name="clientLogger">Where the client logs; <see langword="null"/> discards it.</param>
    /// <returns>The provider under test.</returns>
    private NameAmRegistrarProvider BuildProvider(bool configured = true, ILogger<NameAmClient>? clientLogger = null)
    {
        var options = new NameAmOptions
        {
            Email = configured ? "reseller@example.test" : string.Empty,
            Password = configured ? "secret" : string.Empty,
            ApiUrl = _nameAm.BaseAddress.ToString().TrimEnd('/'),
        };
        var client = new NameAmClient(
            new HttpClient(),
            Options.Create(options),
            _scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            _scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>(),
            _tokens,
            clientLogger ?? NullLogger<NameAmClient>.Instance);
        return new NameAmRegistrarProvider(client, NullLogger<NameAmRegistrarProvider>.Instance, new SystemClock(TimeProvider.System));
    }

    /// <summary>Queues the login reply, then the purchase reply.</summary>
    /// <param name="purchaseJson">The body Name.am answers the purchase with.</param>
    private void ScriptRenewal(string purchaseJson)
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(purchaseJson);
    }

    /// <summary>An unconfigured integration refuses the renewal and calls nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenNotConfigured_FailsWithoutCallingNameAmAsync()
    {
        var provider = BuildProvider(configured: false);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        _nameAm.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// An HTTP 200 whose body reports an error with no recognised cause is a refusal with the
    /// registrar's plain refusal code (a short balance or a taken name has its own code, tested below).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenBodyReportsAnError_FailsWithTheRefusalCodeAsync()
    {
        ScriptRenewal("""{"message":"Invalid renewal plan"}""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>
    /// A successful purchase reports no expiry, so the caller extends the current one; reporting
    /// now + years would cost an early renewal its remaining days.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenPurchased_SucceedsWithoutInventingAnExpiryAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_renew"}]""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 2), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresAt.Should().BeNull();
        var purchase = _nameAm.Requests.Should().Contain(r => r.PathAndQuery.StartsWith("/client/carts/purchase")).Which;
        purchase.Body.Should().Contain("domain_renew").And.Contain("2_year_renew").And.NotContain("register");
    }

    /// <summary>An HTTP 200 that is neither a purchase list nor a known error is not proof of a renewal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenBodyHasAnUnknownShape_FailsAsync()
    {
        ScriptRenewal("""{"success":false,"errors":["plan unavailable"]}""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A 4xx is Name.am turning the order down: a clean refusal, not an exception.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenNameAmAnswers4xx_FailsCleanlyAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.UnprocessableEntity, """{"message":"Insufficient balance"}""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A 5xx leaves the outcome unknown, so it stays an exception for the caller to treat as such.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenNameAmAnswers5xx_ThrowsAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.BadGateway, "{}");
        var provider = BuildProvider();

        var act = () => provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>A registration on an unconfigured integration is refused, so a paid order is refunded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenNotConfigured_FailsWithoutCallingNameAmAsync()
    {
        var provider = BuildProvider(configured: false);

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        _nameAm.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A registration Name.am turns down with a 4xx, for a name the account does not hold, is a
    /// clean refusal.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenNameAmAnswers4xx_FailsCleanlyAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.BadRequest, """{"message":"Domain is taken"}""");
        _nameAm.Enqueue(EmptyListing);
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A purchased registration succeeds and is left for polling to activate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenPurchased_SucceedsPendingPollingAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_registration"}]""");
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistrarRef.Should().Be("example.am");
        result.RequiresPolling.Should().BeTrue();
    }

    /// <summary>A transfer on an unconfigured integration is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferAsync_WhenNotConfigured_FailsAsync()
    {
        var provider = BuildProvider(configured: false);

        var result = await provider.TransferAsync(
            new DomainTransferOrder("example.am", "EPP-1", false, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
    }

    /// <summary>
    /// A change to a registrar-held domain on an unconfigured integration is refused, so the
    /// handler does not save a change the registrar never made.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetNameserversAsync_WhenNotConfigured_FailsAsync()
    {
        var provider = BuildProvider(configured: false);

        var result = await provider.SetNameserversAsync(
            new NameserverChange(new DomainRef("example.am", "example.am"), ["ns1.example.test"]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        _nameAm.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// The registrar has no cancel-transfer operation, so the refusal says so by code rather than
    /// with the registrar's own sentence.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancelTransferAsync_IsRefusedAsUnsupportedAsync()
    {
        var provider = BuildProvider();

        var result = await provider.CancelTransferAsync(new DomainRef("example.com", "REF-1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
        _nameAm.Requests.Should().BeEmpty();
    }

    /// <summary>The body of a domain listing that holds <c>example.am</c>.</summary>
    private const string HeldListing = """{"docs":[{"domain":"example","tld":"am","status":"pending"}]}""";

    /// <summary>The body of a domain listing that holds nothing.</summary>
    private const string EmptyListing = """{"docs":[]}""";

    /// <summary>A registration sends the client's own contact for every role, never a blank one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_SendsTheOrderContactForEveryRoleAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_registration"}]""");
        var provider = BuildProvider();

        await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 2, false, false, "ns1.example.test", null, CompleteContact), CancellationToken.None);

        var purchase = _nameAm.Requests.Single(r => r.PathAndQuery.StartsWith("/client/carts/purchase"));
        purchase.Body.Should().Contain("\"registrantContacts\":{\"firstName\":\"Ani\"")
            .And.Contain("\"administrativeContacts\"").And.Contain("\"technicalContacts\"").And.Contain("\"billingContacts\"")
            .And.Contain("ani@example.test").And.Contain("\"zip\":\"0010\"")
            .And.Contain("2_year_register").And.Contain("\"hostname\":\"ns1.example.test\"");
    }

    /// <summary>A contact missing a required detail is refused before anything is sent to Name.am.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WithIncompleteContact_FailsWithoutCallingNameAmAsync()
    {
        var provider = BuildProvider();
        var incomplete = CompleteContact with { Phone = " " };

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, incomplete), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED);
        _nameAm.Requests.Should().BeEmpty();
    }

    /// <summary>A transfer sends the one year transfer plan, the EPP code and the client's contact.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferAsync_SendsTheTransferPlanAndTheOrderContactAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_transfer"}]""");
        var provider = BuildProvider();

        var result = await provider.TransferAsync(
            new DomainTransferOrder("example.am", "EPP-1", false, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        var purchase = _nameAm.Requests.Single(r => r.PathAndQuery.StartsWith("/client/carts/purchase"));
        purchase.Body.Should().Contain("domain_transfer").And.Contain("1_year_transfer")
            .And.Contain("\"transferCode\":\"EPP-1\"").And.Contain("ani@example.test");
    }

    /// <summary>
    /// In production too, an HTTP 200 that is not a purchase list is no proof of a purchase: the
    /// account is consulted, and when it does not hold the name the order is refused.
    /// </summary>
    /// <param name="purchaseJson">The body Name.am answers the purchase with.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("""{"success":true}""")]
    [InlineData("[]")]
    public async Task RegisterAsync_WhenBodyIsNotAPurchaseList_FailsAsync(string purchaseJson)
    {
        ScriptRenewal(purchaseJson);
        _nameAm.Enqueue(EmptyListing);
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>
    /// A retry that Name.am refuses because the account already holds the name is the earlier
    /// attempt having gone through: success, left for polling, not a refusal that refunds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenRefusedButHeldByTheAccount_SucceedsAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.Conflict, """{"message":"Domain is taken"}""");
        _nameAm.Enqueue(HeldListing);
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistrarRef.Should().Be("example.am");
        result.RequiresPolling.Should().BeTrue();
    }

    /// <summary>A refusal as unavailable for a name the account does not hold has its own code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenRefusedAsUnavailableAndNotHeld_FailsWithTheUnavailableCodeAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.Conflict, """{"message":"Domain is taken"}""");
        _nameAm.Enqueue(EmptyListing);
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE);
    }

    /// <summary>A refusal in the body of a 200 that names the name as taken has the unavailable code too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenBodyReportsTheNameTaken_FailsWithTheUnavailableCodeAsync()
    {
        ScriptRenewal("""{"message":"Domain already registered"}""");
        _nameAm.Enqueue(EmptyListing);
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE);
    }

    /// <summary>A short registrar balance has its own code and the account is not consulted for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenPaymentIsRequired_FailsWithTheFundsCodeAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.PaymentRequired, """{"message":"Insufficient balance"}""");
        var provider = BuildProvider();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.am", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS);
        _nameAm.Requests.Should().HaveCount(2);
    }

    /// <summary>A renewal is never turned into success because the account holds the domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenRefusedAsUnavailable_FailsWithoutLookingAtTheAccountAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.Conflict, """{"message":"Domain is taken"}""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE);
        _nameAm.Requests.Should().HaveCount(2);
    }

    /// <summary>A change Name.am fails with a server error is a failed result carrying a code, not an exception.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarLockAsync_WhenNameAmFails_ReturnsAFailedResultAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.InternalServerError, """{"message":"secret upstream detail"}""");
        var provider = BuildProvider();

        var result = await provider.SetRegistrarLockAsync(
            new RegistrarFlagChange(new DomainRef("example.am", "example.am"), true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A change answered with a body that is not JSON is a failed result.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetWhoisPrivacyAsync_WhenAnswerIsNotJson_ReturnsAFailedResultAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.OK, "<html>maintenance</html>", "text/html");
        var provider = BuildProvider();

        var result = await provider.SetWhoisPrivacyAsync(
            new RegistrarFlagChange(new DomainRef("example.am", "example.am"), true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A DNS change whose listing step fails is a failed result too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecordAsync_WhenTheListingFails_ReturnsAFailedResultAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue(HttpStatusCode.BadGateway, "{}");
        var provider = BuildProvider();
        var record = DnsRecord.CreateDetached(DnsRecordType.A, "www", "192.0.2.1", 1800, null);

        var result = await provider.AddDnsRecordAsync(
            new DnsRecordChange(new DomainRef("example.am", "example.am"), record), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A domain name goes into the URL path escaped, so nothing in it can change the path.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarLockAsync_EscapesTheDomainNameInThePathAsync()
    {
        ScriptRenewal("{}");
        var provider = BuildProvider();

        await provider.SetRegistrarLockAsync(
            new RegistrarFlagChange(new DomainRef("a/b.am", "a/b.am"), true), CancellationToken.None);

        var change = _nameAm.Requests.Single(r => r.Method == "PUT");
        change.PathAndQuery.Should().Contain("a%2Fb.am").And.NotContain("a/b.am");
        change.Body.Should().Be("""{"transferLock":true}""");
    }

    /// <summary>The account e-mail is personal data and is never written to the log.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Login_DoesNotLogTheAccountEmailAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_renew"}]""");
        var logger = new CapturingLogger<NameAmClient>();
        var provider = BuildProvider(clientLogger: logger);

        await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        logger.Messages.Should().NotBeEmpty().And.OnlyContain(m => !m.Contains("reseller@example.test"));
    }

    /// <summary>
    /// Clients are created per request, but the connection has one token: a second client reuses it
    /// and Name.am is logged in to once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoClients_ShareOneLoginAsync()
    {
        ScriptRenewal("""[{"domain":"example.am","type":"domain_renew"}]""");

        await BuildProvider().RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);
        await BuildProvider().RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        _nameAm.Requests.Count(r => r.PathAndQuery.StartsWith("/auth/login")).Should().Be(1);
        _nameAm.Requests.Count(r => r.PathAndQuery.StartsWith("/client/carts/purchase")).Should().Be(2);
    }

    /// <summary>
    /// A 401 means the token is no longer accepted and the request was not processed: a new login
    /// follows and the request is repeated once with the new token.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenTheTokenIsRejected_LogsInAgainAndRepeatsOnceAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer token-a"}""");
        _nameAm.Enqueue(HttpStatusCode.Unauthorized, "{}");
        _nameAm.Enqueue("""{"accessToken":"Bearer token-b"}""");
        _nameAm.Enqueue("""[{"domain":"example.am","type":"domain_renew"}]""");
        var provider = BuildProvider();

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.am", "example.am", 1), CancellationToken.None);

        result.Success.Should().BeTrue();
        _nameAm.Requests.Should().HaveCount(4);
        _nameAm.Requests[1].Authorization.Should().Be("Bearer token-a");
        _nameAm.Requests[3].Authorization.Should().Be("Bearer token-b");
    }

    /// <summary>
    /// Pricing reads a price sent as a number or as a string, and a product or plan of the wrong
    /// shape is skipped without stopping the rest.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricingAsync_ReadsBothPriceShapesAndSkipsBadProductsAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue("""
            [
              {"name": 123, "plans": []},
              {"name": "bad", "plans": "none"},
              {"name": "odd", "plans": [{"behavior": "register", "duration": "one", "currentPrice": {}}]},
              {"name": "am", "plans": [
                {"behavior": "register", "duration": 1, "currentPrice": "5000.50"},
                {"behavior": "renew", "duration": 2, "currentPrice": 9000},
                {"behavior": "transfer", "duration": 1, "currentPrice": "oops"}
              ]}
            ]
            """);
        var provider = BuildProvider();

        var pricing = await provider.GetTldPricingAsync(CancellationToken.None);

        var am = pricing.Single(p => p.Tld == "am");
        am.Currency.Should().Be("AMD");
        am.Register.Should().Equal(new Dictionary<int, decimal> { [1] = 5000.50m });
        am.Renew.Should().Equal(new Dictionary<int, decimal> { [2] = 9000m });
        am.Transfer.Should().BeEmpty();
    }

    /// <summary>Dates Name.am reports without an offset are read as UTC, whatever culture the host runs in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainActiveAsync_ReadsTheExpiryAsUtcAsync()
    {
        ScriptRenewal("""{"docs":[{"domain":"example","tld":"am","status":"active","expiration":"2031-03-15T00:00:00"}]}""");
        var provider = BuildProvider();

        var result = await provider.CheckDomainActiveAsync("example.am", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresAt!.Value.Should().Be(new DateTimeOffset(2031, 3, 15, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>A domain on a later page of the account's list is found, not reported as missing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CheckDomainActiveAsync_FindsADomainOnTheSecondPageAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue("""{"docs":[{"domain":"other","tld":"am","status":"active"}],"page":1,"totalPages":2,"hasNextPage":true}""");
        _nameAm.Enqueue("""{"docs":[{"domain":"example","tld":"am","status":"active","expiration":"2031-03-15T00:00:00"}],"page":2,"totalPages":2,"hasNextPage":false}""");
        var provider = BuildProvider();

        var result = await provider.CheckDomainActiveAsync("example.am", CancellationToken.None);

        result.Success.Should().BeTrue();
        _nameAm.Requests.Should().HaveCount(3);
        _nameAm.Requests[2].PathAndQuery.Should().Contain("page=2");
    }

    /// <summary>A domain absent from every page is reported missing once the last page is read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CheckDomainActiveAsync_ReportsMissingAfterTheLastPageAsync()
    {
        _nameAm.Enqueue("""{"accessToken":"Bearer test-token"}""");
        _nameAm.Enqueue("""{"docs":[{"domain":"other","tld":"am","status":"active"}],"page":1,"totalPages":2}""");
        _nameAm.Enqueue("""{"docs":[],"page":2,"totalPages":2}""");
        var provider = BuildProvider();

        var result = await provider.CheckDomainActiveAsync("example.am", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND);
        _nameAm.Requests.Should().HaveCount(3);
    }
}
