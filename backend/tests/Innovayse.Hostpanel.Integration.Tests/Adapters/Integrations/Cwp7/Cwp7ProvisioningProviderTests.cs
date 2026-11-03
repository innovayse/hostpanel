using System.Net;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Providers.CWP7;
using Innovayse.Providers.CWP7.Models;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Cwp7;

/// <summary>
/// Tests for <see cref="Cwp7ProvisioningProvider"/> against a stub CWP7 server: its failure
/// branches, the retry-safe answers (account already there, already suspended, already gone) and
/// what it keeps out of its logs.
/// </summary>
public sealed class Cwp7ProvisioningProviderTests : IAsyncLifetime
{
    /// <summary>A CWP7 success envelope.</summary>
    private const string Ok = """{ "status": "OK", "msj": "Done." }""";

    /// <summary>A CWP7 refusal envelope whose text must never be repeated.</summary>
    private const string Refused = """{ "status": "Error", "msj": "Unauthorized action for key test-key" }""";

    /// <summary>An account-detail answer for an active account.</summary>
    private const string ActiveDetail =
        """{ "status": "OK", "msj": { "user": "testuser", "domain": "example.com", "suspended": "0" } }""";

    /// <summary>An account-detail answer for a suspended account.</summary>
    private const string SuspendedDetail =
        """{ "status": "OK", "msj": { "user": "testuser", "domain": "example.com", "suspended": "1" } }""";

    /// <summary>A successful answer that holds no such account.</summary>
    private const string NoSuchAccount = """{ "status": "OK", "msj": [] }""";

    /// <summary>A CWP7 error page behind HTTP 200.</summary>
    private const string ErrorPage = "<html><body><h1>Internal Server Error</h1></body></html>";

    /// <summary>The order every provision test sends.</summary>
    private static readonly ProvisionAccountOrder Order = new(7, "example.com", "testuser", "pass123", "default");

    /// <summary>The loopback server standing in for the CWP7 host.</summary>
    private StubHttpServer cwp7 = null!;

    /// <summary>Starts the stub CWP7 server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        cwp7 = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub CWP7 server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await cwp7.DisposeAsync();
    }

    /// <summary>Builds a provider whose CWP7 answers the given bodies in order.</summary>
    /// <param name="logger">The logger the provider writes to.</param>
    /// <param name="answers">The bodies, one per expected call; the last one keeps being served.</param>
    /// <returns>The provider under test.</returns>
    private Cwp7ProvisioningProvider BuildProvider(CapturingLogger<Cwp7ProvisioningProvider> logger, params string[] answers)
    {
        foreach (var answer in answers)
        {
            cwp7.Enqueue(HttpStatusCode.OK, answer, answer.StartsWith('<') ? "text/html" : "application/json");
        }

        var connection = new Cwp7Connection(cwp7.BaseAddress.ToString().TrimEnd('/'), "test-key", "https://cwp.test:2083");
        var client = new Cwp7ApiClient(new HttpClient(), NullLogger<Cwp7ApiClient>.Instance);
        return new Cwp7ProvisioningProvider(connection, "203.0.113.10", client, logger);
    }

    /// <summary>A created account is reported created.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ReturnsCreated_WhenCwp7AcceptsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Ok);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Should().Be(ProvisioningResult.Created("testuser"));
    }

    /// <summary>A retry after a lost answer meets the account it already made.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ReturnsAlreadyExistsWithTheDomain_WhenCwp7RefusesAndTheAccountExistsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, ActiveDetail);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ProvisioningRef.Should().Be("testuser");
        result.ExistingAccountDomain.Should().Be("example.com");
    }

    /// <summary>A refusal with no account behind it is refused, without the panel's text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_IsRefused_WhenCwp7RefusesAndHoldsNoAccountAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, NoSuchAccount);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        result.ErrorMessage.Should().NotContain("Unauthorized").And.NotContain("test-key");
    }

    /// <summary>An error page on creation is a refusal, never a created account.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_IsRefused_WhenCwp7AnswersWithAnErrorPageAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), ErrorPage);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
    }

    /// <summary>The log names the service and never the panel's text or the domain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_LogsOnlyIds_WhenCwp7RefusesAsync()
    {
        var logger = new CapturingLogger<Cwp7ProvisioningProvider>();
        var provider = BuildProvider(logger, Refused, NoSuchAccount);

        await provider.ProvisionAsync(Order, CancellationToken.None);

        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().AllSatisfy(message =>
            message.Should().NotContain("Unauthorized").And.NotContain("example.com").And.NotContain("test-key"));
    }

    /// <summary>Suspending an already suspended account is a success.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_Succeeds_WhenCwp7RefusesBecauseTheAccountIsAlreadySuspendedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, SuspendedDetail);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "overdue"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused suspend on an active account is a real failure.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_Throws_WhenCwp7RefusesAndTheAccountIsActiveAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, ActiveDetail);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "overdue"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain("Unauthorized");
    }

    /// <summary>Unsuspending an already active account is a success.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_Succeeds_WhenCwp7RefusesBecauseTheAccountIsAlreadyActiveAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, ActiveDetail);

        var act = () => provider.UnsuspendAsync("testuser", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused unsuspend on a suspended account is a real failure.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_Throws_WhenCwp7RefusesAndTheAccountIsStillSuspendedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, SuspendedDetail);

        var act = () => provider.UnsuspendAsync("testuser", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Terminating an account that is already gone is a success.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_Succeeds_WhenCwp7RefusesBecauseTheAccountIsAlreadyGoneAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, NoSuchAccount);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "cancelled"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused removal of an account that still exists is a real failure.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_Throws_WhenCwp7RefusesAndTheAccountStillExistsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, ActiveDetail);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "cancelled"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A lookup that cannot be relied on is never read as "already gone".</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_Throws_WhenCwp7RefusesAndTheLookupAnswersWithAnErrorPageAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused, ErrorPage);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "cancelled"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A refused password change fails without repeating the panel's text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePassword_Throws_WhenCwp7RefusesAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused);

        var act = () => provider.ChangePasswordAsync("testuser", "n3w-Secret", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain("Unauthorized");
    }

    /// <summary>An error page on a package change is a failure.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePackage_Throws_WhenCwp7AnswersWithAnErrorPageAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), ErrorPage);

        var act = () => provider.ChangePackageAsync("testuser", "gold", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A refused single sign-on request fails with a fixed phrase.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCPanelSsoUrl_Throws_WhenCwp7IssuesNoSessionAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>(), Refused);

        var act = () => provider.GetCPanelSsoUrlAsync("testuser", "203.0.113.50", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain("Unauthorized");
    }

    /// <summary>The credentials carry no password and the user-panel address from the connection.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCredentials_ReturnsNoPasswordAndTheUserPanelAddressAsync()
    {
        var provider = BuildProvider(new CapturingLogger<Cwp7ProvisioningProvider>());

        var credentials = await provider.GetCredentialsAsync("testuser", CancellationToken.None);

        credentials.Password.Should().BeNull();
        credentials.CpanelUrl.Should().Be("https://cwp.test:2083");
        credentials.ServerIp.Should().Be("203.0.113.10");
    }

    /// <summary>The package-list probe reports an error page as a failure, not a JSON crash.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetServerInfo_Throws_WhenCwp7AnswersWithAnErrorPageAsync()
    {
        cwp7.Enqueue(HttpStatusCode.OK, ErrorPage, "text/html");
        var client = new Cwp7ApiClient(
            new HttpClient(new RedirectToStubHandler(cwp7.BaseAddress)),
            NullLogger<Cwp7ApiClient>.Instance);
        var connection = new HostingPanelConnection("cwp.test", 2304, "test-key");

        var act = () => ((IHostingPanelV7ApiClient)client).GetServerInfoAsync(connection, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
