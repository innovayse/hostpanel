using System.Net;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Plesk;

/// <summary>
/// Tests for <see cref="PleskProvisioningProvider"/> against a stub Plesk server: the packets it
/// sends, its retry-safe answers (account already there, already suspended, already gone), the
/// clean-up of a half-made account and what it keeps out of its logs. The reply bodies are the
/// shapes a Plesk 18.0.82 server answered with.
/// </summary>
public sealed class PleskProvisioningProviderTests : IAsyncLifetime
{
    /// <summary>A customer add, set or del that succeeded.</summary>
    private const string CustomerOk =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><customer><add><result><status>ok</status><id>2</id></result></add></customer></packet>""";

    /// <summary>A refusal whose text names the account and must never be repeated.</summary>
    private const string AlreadyExists =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><customer><add><result><status>error</status><errcode>1007</errcode><errtext>User account testuser already exists.</errtext></result></add></customer></packet>""";

    /// <summary>A webspace refusal Plesk raises with the same "exists" code for a prohibited domain.</summary>
    private const string ProhibitedDomain =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><webspace><add><result><status>error</status><errcode>1007</errcode><errtext>Incorrect name example.com.</errtext></result></add></webspace></packet>""";

    /// <summary>A webspace add that succeeded.</summary>
    private const string SubscriptionOk =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><webspace><add><result><status>ok</status><id>1</id></result></add></webspace></packet>""";

    /// <summary>An active customer; its <c>gen_info</c> status is the account's, not the call's.</summary>
    private const string ActiveCustomer =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><customer><get><result><status>ok</status><id>2</id><data><gen_info><login>testuser</login><status>0</status></gen_info></data></result></get></customer></packet>""";

    /// <summary>A customer disabled by the administrator.</summary>
    private const string SuspendedCustomer =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><customer><get><result><status>ok</status><id>2</id><data><gen_info><login>testuser</login><status>16</status></gen_info></data></result></get></customer></packet>""";

    /// <summary>Plesk's answer for a customer that does not exist.</summary>
    private const string NoSuchCustomer =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><customer><get><result><status>error</status><errcode>1013</errcode><errtext>client does not exist</errtext></result></get></customer></packet>""";

    /// <summary>The customer's subscription.</summary>
    private const string ExistingSubscription =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><webspace><get><result><status>ok</status><id>1</id><data><gen_info><name>example.com</name><status>0</status></gen_info></data></result></get></webspace></packet>""";

    /// <summary>Plesk's answer for a customer that owns no subscription.</summary>
    private const string NoSubscription =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><webspace><get><result><status>ok</status><filter-id>testuser</filter-id></result></get></webspace></packet>""";

    /// <summary>A service plan found by name.</summary>
    private const string PlanFound =
        """<?xml version="1.0" encoding="UTF-8"?><packet version="1.6.9.1"><service-plan><get><result><status>ok</status><id>4</id><name>Unlimited</name><guid>plan-guid-4</guid></result></get></service-plan></packet>""";

    /// <summary>A refusal raised before any operation is read, e.g. a wrong key.</summary>
    private const string WrongKey =
        """<?xml version="1.0"?><packet version="1.6.9.1"><system><status>error</status><errcode>11003</errcode><errtext>key is not found</errtext></system></packet>""";

    /// <summary>The server's addresses as Plesk lists them: a private shared one and an exclusive one.</summary>
    private const string IpList =
        """<packet><ip><get><result><status>ok</status><addresses><ip_info><ip_address>10.0.0.5</ip_address><type>exclusive</type></ip_info><ip_info><ip_address>10.0.0.6</ip_address><type>shared</type></ip_info></addresses></result></get></ip></packet>""";

    /// <summary>The order every provision test sends.</summary>
    private static readonly ProvisionAccountOrder Order = new(7, "example.com", "testuser", "pass<123>&", "Default Domain");

    /// <summary>The loopback server standing in for the Plesk host.</summary>
    private StubHttpServer plesk = null!;

    /// <summary>Starts the stub Plesk server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        plesk = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub Plesk server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await plesk.DisposeAsync();
    }

    /// <summary>Builds a provider whose Plesk answers the given bodies in order.</summary>
    /// <param name="logger">The logger the provider writes to.</param>
    /// <param name="serverIp">The server's own address, or null to make the provider ask Plesk for one.</param>
    /// <param name="answers">The bodies, one per expected call.</param>
    /// <returns>The provider under test.</returns>
    private PleskProvisioningProvider BuildProvider(CapturingLogger<PleskProvisioningProvider> logger, string? serverIp, params string[] answers)
    {
        foreach (var answer in answers)
        {
            plesk.Enqueue(HttpStatusCode.OK, answer, "text/xml");
        }

        var connection = new PleskConnection(plesk.BaseAddress.ToString().TrimEnd('/'), "test-key");
        var client = new PleskApiClient(new HttpClient(), NullLogger<PleskApiClient>.Instance);
        return new PleskProvisioningProvider(connection, serverIp, client, logger);
    }

    /// <summary>A fresh order creates the customer, then the subscription on the order's plan, all to the XML API path.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_CreatesTheCustomerThenTheSubscriptionAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), "10.0.0.5", IpList, CustomerOk, SubscriptionOk);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Created);
        result.ProvisioningRef.Should().Be("testuser");
        plesk.Requests.Should().HaveCount(3).And.OnlyContain(r => r.Method == "POST" && r.PathAndQuery == "/enterprise/control/agent.php");
        plesk.Requests[1].Body.Should().Contain("<customer><add>").And.Contain("<login>testuser</login>");
        plesk.Requests[2].Body.Should().Contain("<webspace><add>")
            .And.Contain("<owner-login>testuser</owner-login>")
            .And.Contain("<ip_address>10.0.0.5</ip_address>")
            .And.Contain("<plan-name>Default Domain</plan-name>");
    }

    /// <summary>A password with XML characters is escaped, not spliced into the packet.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_EscapesXmlCharactersInThePasswordAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, CustomerOk, SubscriptionOk);

        await provider.ProvisionAsync(Order, CancellationToken.None);

        plesk.Requests[1].Body.Should().Contain("<passwd>pass&lt;123&gt;&amp;</passwd>");
    }

    /// <summary>
    /// An address staff entered that Plesk does not hold (a public address in front of NAT) is not
    /// used; the first shared address Plesk lists hosts the subscription instead.
    /// </summary>
    /// <param name="serverIp">The address on the server row.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("203.0.113.10")]
    public async Task Provision_UsesTheFirstSharedIp_WhenPleskDoesNotHoldTheServersAddressAsync(string? serverIp)
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), serverIp, IpList, CustomerOk, SubscriptionOk);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Created);
        plesk.Requests[2].Body.Should().Contain("<ip_address>10.0.0.6</ip_address>");
    }

    /// <summary>A retry that meets a complete account reports it with its subscription's domain, for the caller to adopt or refuse.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ReportsAlreadyExists_WhenTheAccountIsAlreadyCompleteAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, AlreadyExists, ActiveCustomer, ExistingSubscription);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ExistingAccountDomain.Should().Be("example.com");
        plesk.Requests.Should().HaveCount(4);
    }

    /// <summary>
    /// A customer that already holds the username but has no subscription is reported with no
    /// domain, and nothing is added to it: it may be a stranger's, and building on it would hand
    /// them the client's site.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_NeverBuildsOnAnExistingCustomerAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, AlreadyExists, ActiveCustomer, NoSubscription);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ExistingAccountDomain.Should().BeNull();
        plesk.Requests.Should().NotContain(r => r.Body.Contains("<webspace><add>") || r.Body.Contains("<del>"));
    }

    /// <summary>A refused customer that does not exist afterwards is a refusal, not an adoption.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_IsRefused_WhenTheCustomerCannotBeCreatedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, AlreadyExists, NoSuchCustomer);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        result.ErrorMessage.Should().NotContain("testuser");
    }

    /// <summary>
    /// A positively refused subscription removes the customer this call made, so nothing is left on
    /// the panel, and Plesk's "exists" code for a prohibited domain is not read as an existing account.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_RemovesTheNewCustomer_WhenTheSubscriptionIsRefusedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, CustomerOk, ProhibitedDomain, NoSubscription, CustomerOk);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        plesk.Requests[4].Body.Should().Contain("<customer><del>").And.Contain("<login>testuser</login>");
    }

    /// <summary>
    /// A subscription whose answer was lost is not a refusal: the customer is kept, since the
    /// subscription may well exist, and the retry finds it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_KeepsTheNewCustomer_WhenTheSubscriptionAnswerIsLostAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, IpList, CustomerOk, "<html>Bad Gateway</html>", "<html>Bad Gateway</html>");

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        plesk.Requests.Should().NotContain(r => r.Body.Contains("<del>"));
    }

    /// <summary>Suspending sets the customer's status to "disabled by administrator".</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_DisablesTheCustomerAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, CustomerOk);

        await provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue"), CancellationToken.None);

        plesk.Requests.Should().ContainSingle().Which.Body.Should().Contain("<customer><set>").And.Contain("<status>16</status>");
    }

    /// <summary>A refused suspend of a customer that is already disabled succeeds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_Succeeds_WhenTheCustomerIsAlreadyDisabledAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, WrongKey, SuspendedCustomer);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused suspend of an active customer fails, without the panel's text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_Throws_WhenTheCustomerIsStillActiveAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, WrongKey, ActiveCustomer);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain("key is not found");
    }

    /// <summary>Unsuspending sets the customer's status back to active.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_ReEnablesTheCustomerAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, CustomerOk);

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        plesk.Requests.Should().ContainSingle().Which.Body.Should().Contain("<status>0</status>");
    }

    /// <summary>A refused terminate of a customer Plesk positively reports gone succeeds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_Succeeds_WhenTheCustomerIsAlreadyGoneAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, NoSuchCustomer, NoSuchCustomer);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "Cancelled"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused terminate whose lookup cannot be read is a failure, never "already gone".</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_Throws_WhenTheAbsenceIsUnconfirmedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, WrongKey, "<html>Bad Gateway</html>");

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "Cancelled"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>The credentials carry the subscription's domain and the panel address, never a password.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCredentials_ReadsTheDomainFromTheSubscriptionAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), "203.0.113.10", ExistingSubscription);

        var credentials = await provider.GetCredentialsAsync("testuser", CancellationToken.None);

        credentials.Domain.Should().Be("example.com");
        credentials.Password.Should().BeNull();
        credentials.ServerIp.Should().Be("203.0.113.10");
        credentials.CpanelUrl.Should().Be(plesk.BaseAddress.ToString().TrimEnd('/'));
    }

    /// <summary>Credentials for an account with no subscription fail instead of inventing a domain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCredentials_Throws_WhenTheSubscriptionIsMissingAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, NoSubscription);

        var act = () => provider.GetCredentialsAsync("testuser", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Changing the password changes it for the panel customer and for the subscription's system user.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePassword_ChangesThePanelAndTheSystemUserAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, CustomerOk, SubscriptionOk);

        await provider.ChangePasswordAsync("testuser", "N3w&pass", CancellationToken.None);

        plesk.Requests.Should().HaveCount(2);
        plesk.Requests[0].Body.Should().Contain("<customer><set>").And.Contain("<passwd>N3w&amp;pass</passwd>");
        plesk.Requests[1].Body.Should().Contain("<webspace><set>")
            .And.Contain("<owner-login>testuser</owner-login>")
            .And.Contain("<name>ftp_password</name><value>N3w&amp;pass</value>");
    }

    /// <summary>Changing the package looks the plan up by name and switches the subscription to its guid.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePackage_SwitchesToThePlanGuidAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, PlanFound, SubscriptionOk);

        await provider.ChangePackageAsync("testuser", "Unlimited", CancellationToken.None);

        plesk.Requests[1].Body.Should().Contain("<switch-subscription>").And.Contain("<plan-guid>plan-guid-4</plan-guid>");
    }

    /// <summary>Changing to a plan the server does not have fails before any switch is sent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePackage_Throws_WhenThePlanDoesNotExistAsync()
    {
        const string noPlan =
            """<packet><service-plan><get><result><status>error</status><errcode>1013</errcode><errtext>There is no such a service plan</errtext></result></get></service-plan></packet>""";
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null, noPlan);

        var act = () => provider.ChangePackageAsync("testuser", "Nope", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        plesk.Requests.Should().ContainSingle();
    }

    /// <summary>Single sign-on is refused without calling the panel, since its session is bound to the browser's address.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetSsoUrl_IsNotSupportedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<PleskProvisioningProvider>(), null);

        var act = () => provider.GetCPanelSsoUrlAsync("testuser", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
        plesk.Requests.Should().BeEmpty();
    }

    /// <summary>No log line repeats the panel's error text or the order's domain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Logs_NeverCarryThePanelTextOrTheDomainAsync()
    {
        var logger = new CapturingLogger<PleskProvisioningProvider>();
        var provider = BuildProvider(logger, null, IpList, CustomerOk, ProhibitedDomain, NoSubscription, CustomerOk);

        await provider.ProvisionAsync(Order, CancellationToken.None);

        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(m => m.Contains("Incorrect name") || m.Contains("example.com"));
    }
}
