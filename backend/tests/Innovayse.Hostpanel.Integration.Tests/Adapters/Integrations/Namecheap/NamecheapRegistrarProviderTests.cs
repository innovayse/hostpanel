using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.Namecheap;
using Innovayse.Hostpanel.Infrastructure.Integrations.Namecheap.Settings;
using Innovayse.Hostpanel.Infrastructure.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Namecheap;

/// <summary>
/// Unit tests for <see cref="NamecheapRegistrarProvider"/>'s contact updates, email forwarding
/// and DNS-management toggle, verifying both the
/// XML response parsing and the exact query parameters sent to the Namecheap API.
/// </summary>
public sealed class NamecheapRegistrarProviderTests : IAsyncLifetime
{
    /// <summary>The moment the clock of the provider built by <c>BuildProvider</c> stands at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The loopback server standing in for the Namecheap API; started per test class instance.</summary>
    private StubHttpServer namecheap = null!;

    /// <summary>Starts the stub Namecheap server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        namecheap = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub Namecheap server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await namecheap.DisposeAsync();
    }

    /// <summary>Gets settings whose API URL points at the stub server.</summary>
    private NamecheapOptions Settings => new()
    {
        ApiUser = "testuser",
        ApiKey = "testkey",
        ClientIp = "127.0.0.1",
        ApiUrl = new Uri(namecheap.BaseAddress, "xml.response").ToString(),
    };

    /// <summary>
    /// Builds a provider aimed at the stub server, which answers <paramref name="responseXml"/> over
    /// real HTTP; the requests it received are read back from <see cref="namecheap"/>.
    /// </summary>
    /// <param name="responseXml">The XML body Namecheap answers with.</param>
    /// <returns>The provider under test.</returns>
    private NamecheapRegistrarProvider BuildProvider(string responseXml)
    {
        namecheap.Enqueue(HttpStatusCode.OK, responseXml, "text/xml");
        return new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(Settings)),
            NullLogger<NamecheapRegistrarProvider>.Instance,
            new SystemClock(new ManualTimeProvider(Now)));
    }

    /// <summary>Gets the most recent request the stub received.</summary>
    /// <returns>The recorded request.</returns>
    private RecordedCall LastCall()
    {
        return namecheap.Requests.Last();
    }

    /// <summary>
    /// Extracts a single decoded value from a request's form body. Namecheap commands travel as a
    /// POST form, so every parameter, the credentials included, is read from the body.
    /// </summary>
    /// <param name="call">The recorded request.</param>
    /// <param name="name">Name of the form field.</param>
    /// <returns>The decoded value of the field, or an empty string when it is absent.</returns>
    private static string FormParam(RecordedCall call, string name)
    {
        foreach (var pair in call.Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0].Replace('+', ' ')) == name)
            {
                return Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }
        }

        return string.Empty;
    }

    /// <summary>Extracts a decoded form field from the most recent request.</summary>
    /// <param name="name">Name of the form field.</param>
    /// <returns>The decoded value of the field.</returns>
    private string FormParam(string name)
    {
        return FormParam(LastCall(), name);
    }

    /// <summary>A contact with every detail a purchase needs.</summary>
    private static readonly DomainContact CompleteContact = new(
        "Jane", "Doe", "Acme Inc", "jane@example.com", "+1.5551234567",
        "123 Main St", null, "Springfield", "IL", "62701", "US");

    /// <summary>Modifying contact details sends the set-contacts command for all four contact roles.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContactDetailsAsync_SendsSetContactsCommand_ForAllFourRolesAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainSetContactResult Domain="example.com" IsSuccess="true" /></CommandResponse></ApiResponse>""";
        var provider = BuildProvider(response);

        var contact = new DomainContact(
            "Jane", "Doe", "Acme Inc", "jane@example.com", "+1.5551234567",
            "123 Main St", null, "Springfield", "IL", "62701", "US");

        var result = await provider.ModifyContactDetailsAsync(new DomainContactChange("example.com", contact), CancellationToken.None);

        result.Success.Should().BeTrue();
        FormParam("Command").Should().Be("namecheap.domains.setContacts");
        FormParam("RegistrantFirstName").Should().Be("Jane");
        FormParam("TechEmailAddress").Should().Be("jane@example.com");
        FormParam("AdminCountry").Should().Be("US");
        FormParam("AuxBillingLastName").Should().Be("Doe");
    }

    /// <summary>Reading contact details maps the registrant block of the get-contacts response.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContactDetailsAsync_MapsTheRegistrantBlockAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainContactsResult Domain="example.com"><Registrant><FirstName>Jane</FirstName><LastName>Doe</LastName><OrganizationName></OrganizationName><EmailAddress>jane@example.com</EmailAddress><Phone>+1.5551234567</Phone><Address1>123 Main St</Address1><Address2 /><City>Springfield</City><StateProvince>IL</StateProvince><PostalCode>62701</PostalCode><Country>US</Country></Registrant><Tech><FirstName>Other</FirstName></Tech></DomainContactsResult></CommandResponse></ApiResponse>""";
        var provider = BuildProvider(response);

        var contact = await provider.GetContactDetailsAsync("example.com", CancellationToken.None);

        FormParam("Command").Should().Be("namecheap.domains.getContacts");
        contact.Should().Be(new DomainContact(
            "Jane", "Doe", null, "jane@example.com", "+1.5551234567",
            "123 Main St", null, "Springfield", "IL", "62701", "US"));
    }

    /// <summary>Reading contact details answers null when the response has no registrant.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContactDetailsAsync_WithoutRegistrantBlock_ReturnsNullAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""";
        var provider = BuildProvider(response);

        var contact = await provider.GetContactDetailsAsync("example.com", CancellationToken.None);

        contact.Should().BeNull();
    }

    /// <summary>Disabling email forwarding clears all forwards.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetEmailForwardingAsync_WhenDisabling_ClearsAllForwardsAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""";
        var provider = BuildProvider(response);

        var result = await provider.SetEmailForwardingAsync(new DomainFeatureToggle("example.com", false), CancellationToken.None);

        result.Success.Should().BeTrue();
        FormParam("Command").Should().Be("namecheap.domains.dns.setEmailForwarding");
        FormParam("MailBox1").Should().BeEmpty();
    }

    /// <summary>Adding an email forwarding rule merges it with the existing forwards.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRuleAsync_MergesWithExistingForwardsAsync()
    {
        // First call (GetEmailForwardsAsync inside AddEmailForwardingRuleAsync) returns one
        // existing forward; the provider must resend it alongside the newly added rule.
        const string getResponse = """
            <?xml version="1.0"?>
            <ApiResponse Status="OK">
              <CommandResponse>
                <DomainDNSGetEmailForwardingResult>
                  <Forward mailbox="sales">sales@destination.com</Forward>
                </DomainDNSGetEmailForwardingResult>
              </CommandResponse>
            </ApiResponse>
            """;

        var provider = BuildProvider(getResponse);

        var result = await provider.AddEmailForwardingRuleAsync(
            new EmailForwardingRuleChange("example.com", "support", "support@destination.com"), CancellationToken.None);

        result.Success.Should().BeTrue();
        namecheap.Requests.Should().HaveCount(2);

        var setCall = namecheap.Requests[1];
        FormParam(setCall, "Command").Should().Be("namecheap.domains.dns.setEmailForwarding");

        var mailboxes = new[] { FormParam(setCall, "MailBox1"), FormParam(setCall, "MailBox2") };
        mailboxes.Should().Contain("sales").And.Contain("support");
    }

    /// <summary>Enabling DNS management calls set-default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDnsManagementAsync_WhenEnabling_CallsSetDefaultAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""";
        var provider = BuildProvider(response);

        var result = await provider.SetDnsManagementAsync(new DomainFeatureToggle("example.com", true), CancellationToken.None);

        result.Success.Should().BeTrue();
        FormParam("Command").Should().Be("namecheap.domains.dns.setDefault");
    }

    /// <summary>Disabling DNS management makes no API call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDnsManagementAsync_WhenDisabling_MakesNoApiCallAsync()
    {
        var provider = BuildProvider("""<ApiResponse Status="OK"/>""");

        var result = await provider.SetDnsManagementAsync(new DomainFeatureToggle("example.com", false), CancellationToken.None);

        result.Success.Should().BeTrue();
        namecheap.Requests.Should().BeEmpty();
    }

    /// <summary>An unconfigured integration refuses a renewal and calls nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenNotConfigured_FailsWithoutCallingNamecheapAsync()
    {
        var unconfigured = Settings;
        unconfigured.ApiKey = string.Empty;
        var provider = new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(unconfigured)),
            NullLogger<NamecheapRegistrarProvider>.Instance,
            new SystemClock(TimeProvider.System));

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        namecheap.Requests.Should().BeEmpty();
    }

    /// <summary>A renewal answer with an expiry reports that expiry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WithExpireDate_ReportsItAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainRenewResult DomainName="example.com" Renew="true" ExpireDate="2029-03-15" /></CommandResponse></ApiResponse>""";
        var provider = BuildProvider(response);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresAt!.Value.Date.Should().Be(new DateTime(2029, 3, 15));
        FormParam("Command").Should().Be("namecheap.domains.renew");
    }

    /// <summary>
    /// A renewal answer without an expiry reports none, so the caller extends the current one;
    /// now + years would cost an early renewal its remaining days.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WithoutExpireDate_ReportsNoExpiryAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainRenewResult DomainName="example.com" Renew="true" /></CommandResponse></ApiResponse>""";
        var provider = BuildProvider(response);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresAt.Should().BeNull();
    }

    /// <summary>An OK envelope whose renew result is not <c>Renew="true"</c> renewed nothing.</summary>
    /// <param name="response">The Namecheap answer.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainRenewResult DomainName="example.com" Renew="false" /></CommandResponse></ApiResponse>""")]
    [InlineData("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""")]
    public async Task RenewAsync_WithoutRenewConfirmation_FailsAsync(string response)
    {
        var provider = BuildProvider(response);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>The answer Namecheap gives a command it refuses.</summary>
    private const string ErrorResponse = """<?xml version="1.0"?><ApiResponse Status="ERROR"><Errors><Error Number="2033409">Domain is not available</Error></Errors></ApiResponse>""";

    /// <summary>Builds a provider whose settings have no API key.</summary>
    /// <returns>The unconfigured provider.</returns>
    private NamecheapRegistrarProvider BuildUnconfiguredProvider()
    {
        var unconfigured = Settings;
        unconfigured.ApiKey = string.Empty;
        return new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(unconfigured)),
            NullLogger<NamecheapRegistrarProvider>.Instance,
            new SystemClock(TimeProvider.System));
    }

    /// <summary>A registration on an unconfigured integration is refused, so a paid order is refunded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenNotConfigured_FailsAsync()
    {
        var result = await BuildUnconfiguredProvider().RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        namecheap.Requests.Should().BeEmpty();
    }

    /// <summary>An ERROR answer is a clean refusal with the registrar's refusal code, not an exception.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenNamecheapAnswersError_FailsCleanlyAsync()
    {
        var provider = BuildProvider(ErrorResponse);

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>An OK envelope without <c>Registered="true"</c> registered nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WithoutRegisteredConfirmation_FailsAsync()
    {
        var provider = BuildProvider("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainCreateResult Domain="example.com" Registered="false" /></CommandResponse></ApiResponse>""");

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>
    /// A confirmed registration without a <c>DomainID</c> is referenced by its name, which every
    /// later Namecheap call addresses it by, so activation never receives an empty reference.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WithoutDomainId_FallsBackToTheDomainNameAsync()
    {
        var provider = BuildProvider("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainCreateResult Domain="example.com" Registered="true" /></CommandResponse></ApiResponse>""");

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 2, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistrarRef.Should().Be("example.com");
    }

    /// <summary>A transfer on an unconfigured integration is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferAsync_WhenNotConfigured_FailsAsync()
    {
        var result = await BuildUnconfiguredProvider().TransferAsync(
            new DomainTransferOrder("example.com", "EPP-1", false, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
    }

    /// <summary>A transfer Namecheap refuses with ERROR is a clean refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferAsync_WhenNamecheapAnswersError_FailsCleanlyAsync()
    {
        var result = await BuildProvider(ErrorResponse).TransferAsync(
            new DomainTransferOrder("example.com", "EPP-1", false, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>
    /// A renewal Namecheap refuses with ERROR is a clean refusal, so the paid-renewal engine
    /// releases the claim and retries instead of parking it as an unknown outcome.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenNamecheapAnswersError_FailsCleanlyAsync()
    {
        var result = await BuildProvider(ErrorResponse).RenewAsync(
            new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A change to a registrar-held domain on an unconfigured integration is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetNameserversAsync_WhenNotConfigured_FailsAsync()
    {
        var result = await BuildUnconfiguredProvider().SetNameserversAsync(
            new NameserverChange(new DomainRef("example.com", "example.com"), ["ns1.example.test"]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
    }

    /// <summary>
    /// A DNS record added on an unconfigured integration is refused, so the handler does not save
    /// a record Namecheap never got.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecordAsync_WhenNotConfigured_FailsAsync()
    {
        var record = DnsRecord.CreateDetached(DnsRecordType.A, "www", "192.0.2.1", 1800, null);

        var result = await BuildUnconfiguredProvider().AddDnsRecordAsync(
            new DnsRecordChange(new DomainRef("example.com", "example.com"), record), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        namecheap.Requests.Should().BeEmpty();
    }

    /// <summary>The client refuses to answer an unconfigured call with a fake OK envelope.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Client_WhenNotConfigured_ThrowsInsteadOfFakingSuccessAsync()
    {
        var unconfigured = Settings;
        unconfigured.ApiKey = string.Empty;
        var client = new NamecheapClient(new HttpClient(), Options.Create(unconfigured));

        var act = () => client.ExecuteAsync(new NamecheapCommand("namecheap.domains.getList", new Dictionary<string, string>()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// The registrar has no cancel-transfer operation, so the refusal says so by code rather than
    /// with the registrar's own sentence.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancelTransferAsync_IsRefusedAsUnsupportedAsync()
    {
        var provider = BuildUnconfiguredProvider();

        var result = await provider.CancelTransferAsync(new DomainRef("example.com", "REF-1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
    }

    /// <summary>
    /// The API key and the account name travel in the POST form body and never in the URL; only
    /// the command name is in the query string.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExecuteAsync_SendsCredentialsInTheBodyNotTheUrlAsync()
    {
        var provider = BuildProvider("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""");

        await provider.SetDnsManagementAsync(new DomainFeatureToggle("example.com", true), CancellationToken.None);

        var call = LastCall();
        call.Method.Should().Be("POST");
        call.PathAndQuery.Should().NotContain("testkey").And.NotContain("testuser");
        call.PathAndQuery.Should().EndWith("?Command=namecheap.domains.dns.setDefault");
        FormParam(call, "ApiKey").Should().Be("testkey");
        FormParam(call, "UserName").Should().Be("testuser");
        FormParam(call, "ApiUser").Should().Be("testuser");
        FormParam(call, "ClientIp").Should().Be("127.0.0.1");
    }

    /// <summary>The caller's command is only read: the credentials are added to the request, not to its parameters.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExecuteAsync_DoesNotChangeTheCallersParametersAsync()
    {
        namecheap.Enqueue(HttpStatusCode.OK, """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""", "text/xml");
        var client = new NamecheapClient(new HttpClient(), Options.Create(Settings));
        var parameters = new Dictionary<string, string> { ["SLD"] = "example", ["ApiKey"] = "from-the-caller" };

        await client.ExecuteAsync(new NamecheapCommand("namecheap.domains.getInfo", parameters), CancellationToken.None);

        parameters.Should().BeEquivalentTo(new Dictionary<string, string> { ["SLD"] = "example", ["ApiKey"] = "from-the-caller" });
        FormParam("ApiKey").Should().Be("testkey");
    }

    /// <summary>An answer that is not XML is an unknown outcome for the client: an HTTP failure, not a parse error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExecuteAsync_WhenAnswerIsNotXml_ThrowsHttpRequestExceptionAsync()
    {
        namecheap.Enqueue(HttpStatusCode.OK, "<html>bad gateway", "text/html");
        var client = new NamecheapClient(new HttpClient(), Options.Create(Settings));

        var act = () => client.ExecuteAsync(
            new NamecheapCommand("namecheap.domains.getInfo", new Dictionary<string, string>()), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>A registration sends the client's contact for every role.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_SendsTheOrderContactForEveryRoleAsync()
    {
        var provider = BuildProvider("""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainCreateResult Domain="example.com" Registered="true" /></CommandResponse></ApiResponse>""");

        await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        FormParam("Command").Should().Be("namecheap.domains.create");
        FormParam("RegistrantEmailAddress").Should().Be("jane@example.com");
        FormParam("TechPhone").Should().Be("+1.5551234567");
        FormParam("AdminAddress1").Should().Be("123 Main St");
        FormParam("AuxBillingCountry").Should().Be("US");
    }

    /// <summary>A contact missing a required detail is refused before anything is sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WithIncompleteContact_FailsWithoutCallingNamecheapAsync()
    {
        var provider = BuildProvider("""<?xml version="1.0"?><ApiResponse Status="OK"/>""");
        var incomplete = CompleteContact with { Email = string.Empty };

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, incomplete), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED);
        namecheap.Requests.Should().BeEmpty();
    }

    /// <summary>An expiry with no offset is read as UTC, and an unreadable one falls back to now plus the term.</summary>
    /// <param name="expireDate">The <c>ExpireDate</c> Namecheap answers with.</param>
    /// <param name="expectedYear">The year the reported expiry falls in.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("2031-03-15T00:00:00", 2031)]
    [InlineData("not a date", 0)]
    public async Task RegisterAsync_ReadsTheExpiryInvariantlyAsync(string expireDate, int expectedYear)
    {
        var provider = BuildProvider($"""<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainCreateResult Domain="example.com" Registered="true" ExpireDate="{expireDate}" /></CommandResponse></ApiResponse>""");

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 2, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        if (expectedYear != 0)
        {
            result.ExpiresAt!.Value.Offset.Should().Be(TimeSpan.Zero);
            result.ExpiresAt.Value.Year.Should().Be(expectedYear);
        }
        else
        {
            result.ExpiresAt!.Value.Should().Be(Now.AddYears(2));
        }
    }

    /// <summary>The answer Namecheap gives when the name is not available to register (error 2030280).</summary>
    private const string UnavailableResponse = """<?xml version="1.0"?><ApiResponse Status="ERROR"><Errors><Error Number="2030280">Domain not available</Error></Errors></ApiResponse>""";

    /// <summary>The answer Namecheap gives when the account balance is too low (error 2019166).</summary>
    private const string InsufficientFundsResponse = """<?xml version="1.0"?><ApiResponse Status="ERROR"><Errors><Error Number="2019166">Insufficient funds</Error></Errors></ApiResponse>""";

    /// <summary>A domain list that holds the domain, with the expiry in the format Namecheap lists it.</summary>
    private const string HeldListResponse = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainGetListResult><Domain ID="777" Name="example.com" Expires="04/12/2031" /></DomainGetListResult></CommandResponse></ApiResponse>""";

    /// <summary>A domain list that holds nothing by that name.</summary>
    private const string EmptyListResponse = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainGetListResult /></CommandResponse></ApiResponse>""";

    /// <summary>
    /// A retry answered "domain unavailable" for a name the account already holds is the earlier
    /// attempt having gone through, so it is success and nothing is refunded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenUnavailableButHeldByTheAccount_SucceedsAsync()
    {
        var provider = BuildProvider(UnavailableResponse);
        namecheap.Enqueue(HttpStatusCode.OK, HeldListResponse, "text/xml");

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistrarRef.Should().Be("777");
        result.ExpiresAt!.Value.Date.Should().Be(new DateTime(2031, 4, 12));
        namecheap.Requests.Select(r => FormParam(r, "Command")).Should()
            .Equal("namecheap.domains.create", "namecheap.domains.getList");
    }

    /// <summary>"Domain unavailable" for a name the account does not hold is a refusal with its own code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenUnavailableAndNotHeld_FailsWithTheUnavailableCodeAsync()
    {
        var provider = BuildProvider(UnavailableResponse);
        namecheap.Enqueue(HttpStatusCode.OK, EmptyListResponse, "text/xml");

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE);
    }

    /// <summary>Too little registrar balance is its own code, and the account is not consulted for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenFundsAreShort_FailsWithTheFundsCodeAsync()
    {
        var provider = BuildProvider(InsufficientFundsResponse);

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS);
        namecheap.Requests.Should().HaveCount(1);
    }

    /// <summary>
    /// When the account cannot be read to settle a refusal, the outcome is unknown and the
    /// exception escapes instead of a refusal that could refund a domain we own.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterAsync_WhenTheAccountCannotBeReadAfterARefusal_ThrowsAsync()
    {
        var provider = BuildProvider(UnavailableResponse);
        namecheap.Enqueue(HttpStatusCode.BadGateway, "bad gateway", "text/plain");

        var act = () => provider.RegisterAsync(
            new DomainRegistrationOrder("example.com", 1, false, false, null, null, CompleteContact), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>A transfer that is refused as unavailable but is already in progress counts as placed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferAsync_WhenUnavailableButInProgress_SucceedsAsync()
    {
        var provider = BuildProvider(UnavailableResponse);
        namecheap.Enqueue(HttpStatusCode.OK, EmptyListResponse, "text/xml");
        namecheap.Enqueue(HttpStatusCode.OK, """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainTransferGetListResult><Transfer ID="9" DomainName="example.com" /></DomainTransferGetListResult></CommandResponse></ApiResponse>""", "text/xml");

        var result = await provider.TransferAsync(
            new DomainTransferOrder("example.com", "EPP-1", false, CompleteContact), CancellationToken.None);

        result.Success.Should().BeTrue();
        namecheap.Requests.Select(r => FormParam(r, "Command")).Should().Equal(
            "namecheap.domains.transfer.create", "namecheap.domains.getList", "namecheap.domains.transfer.getList");
        FormParam(namecheap.Requests[2], "ListType").Should().Be("INPROGRESS");
    }

    /// <summary>
    /// A refused renewal stays a refusal even when the account holds the domain, because holding
    /// it says nothing about whether it was renewed; no lookup is made.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewAsync_WhenUnavailable_FailsWithoutLookingAtTheAccountAsync()
    {
        var provider = BuildProvider(UnavailableResponse);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.com", "example.com", 1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE);
        namecheap.Requests.Should().HaveCount(1);
    }

    /// <summary>A lock change Namecheap fails with a server error is a failed result, not an exception, and no upstream text is returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarLockAsync_WhenNamecheapFails_ReturnsAFailedResultAsync()
    {
        namecheap.Enqueue(HttpStatusCode.InternalServerError, "secret upstream detail", "text/plain");
        var provider = new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(Settings)),
            NullLogger<NamecheapRegistrarProvider>.Instance,
            new SystemClock(TimeProvider.System));

        var result = await provider.SetRegistrarLockAsync(
            new RegistrarFlagChange(new DomainRef("example.com", "example.com"), true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A change answered with something that is not XML is a failed result.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetNameserversAsync_WhenAnswerIsNotXml_ReturnsAFailedResultAsync()
    {
        namecheap.Enqueue(HttpStatusCode.OK, "<html>maintenance", "text/html");
        var provider = new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(Settings)),
            NullLogger<NamecheapRegistrarProvider>.Instance,
            new SystemClock(TimeProvider.System));

        var result = await provider.SetNameserversAsync(
            new NameserverChange(new DomainRef("example.com", "example.com"), ["ns1.example.test"]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A refusal inside a DNS change is a failed result with a code, and the log names the operation that failed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecordAsync_WhenNamecheapRefuses_ReturnsTheCodeAndLogsTheOperationAsync()
    {
        namecheap.Enqueue(HttpStatusCode.OK, """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainDNSGetHostsResult /></CommandResponse></ApiResponse>""", "text/xml");
        namecheap.Enqueue(HttpStatusCode.OK, """<?xml version="1.0"?><ApiResponse Status="ERROR"><Errors><Error Number="1011102">API key is invalid</Error></Errors></ApiResponse>""", "text/xml");
        var logger = new CapturingLogger<NamecheapRegistrarProvider>();
        var provider = new NamecheapRegistrarProvider(
            new NamecheapClient(new HttpClient(), Options.Create(Settings)), logger, new SystemClock(TimeProvider.System));
        var record = DnsRecord.CreateDetached(DnsRecordType.A, "www", "192.0.2.1", 1800, null);

        var result = await provider.AddDnsRecordAsync(
            new DnsRecordChange(new DomainRef("example.com", "example.com"), record), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
        logger.Messages.Should().Contain(m => m.Contains("adding a DNS record"));
    }
}
