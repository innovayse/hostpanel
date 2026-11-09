using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Providers.CWP;
using Innovayse.Providers.CWP.Models;
using Innovayse.Providers.CWP.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.CWP.Tests;

/// <summary>
/// Tests for <see cref="CwpProvisioningProvider"/> against a stub CWP server: typed answers for
/// a refusal that really means "the account is already like that", and for every other refusal.
/// </summary>
public sealed class CwpProvisioningProviderTests : IAsyncLifetime
{
    /// <summary>A CWP success envelope.</summary>
    private const string Ok = """{ "status": "OK", "msj": "Done." }""";

    /// <summary>A CWP refusal envelope.</summary>
    private const string Refused = """{ "status": "Error", "msj": "denied" }""";

    /// <summary>A listing holding one active account.</summary>
    private const string ActiveListing =
        """{ "status": "OK", "msj": [ { "user": "testuser", "domain": "example.com", "suspended": "0" } ] }""";

    /// <summary>A listing holding one suspended account.</summary>
    private const string SuspendedListing =
        """{ "status": "OK", "msj": [ { "user": "testuser", "domain": "example.com", "suspended": "1" } ] }""";

    /// <summary>A listing holding no accounts.</summary>
    private const string EmptyListing = """{ "status": "OK", "msj": [] }""";

    /// <summary>The account every create test asks for.</summary>
    private static readonly CwpNewAccount NewAccount = new("example.com", "testuser", "pass123", "default", "a@example.com");

    /// <summary>The loopback server standing in for the CWP host.</summary>
    private StubHttpServer cwp = null!;

    /// <summary>Starts the stub CWP server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        cwp = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub CWP server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await cwp.DisposeAsync();
    }

    /// <summary>Builds a provider whose CWP answers the given bodies in order.</summary>
    /// <param name="answers">The bodies, one per expected call; the last one keeps being served.</param>
    /// <returns>The provider under test.</returns>
    private CwpProvisioningProvider BuildProvider(params string[] answers)
    {
        foreach (var answer in answers)
        {
            cwp.Enqueue(answer);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["integration:innovayse-cwp:host"] = "cwp.test",
                ["integration:innovayse-cwp:api_key"] = "test-key",
            })
            .Build();

        return new CwpProvisioningProvider(
            configuration,
            NullLogger<CwpProvisioningProvider>.Instance,
            NullLoggerFactory.Instance,
            new StubHttpClientFactory(cwp));
    }

    /// <summary>A created account is reported as succeeded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateAccount_Succeeds_WhenCwpAcceptsAsync()
    {
        var provider = BuildProvider(Ok);

        var result = await provider.CreateAccountAsync(NewAccount, CancellationToken.None);

        result.Should().Be(new CwpAccountResult(CwpAccountOutcome.Succeeded, null));
    }

    /// <summary>A retry after a lost answer meets the account it already made.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateAccount_ReportsAlreadyExistsWithTheDomain_WhenCwpRefusesAndTheAccountIsListedAsync()
    {
        var provider = BuildProvider(Refused, ActiveListing);

        var result = await provider.CreateAccountAsync(NewAccount, CancellationToken.None);

        result.Should().Be(new CwpAccountResult(CwpAccountOutcome.AlreadyExists, "example.com"));
    }

    /// <summary>A refusal with no account behind it stays refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateAccount_IsRefused_WhenCwpRefusesAndListsNoAccountAsync()
    {
        var provider = BuildProvider(Refused, EmptyListing);

        var result = await provider.CreateAccountAsync(NewAccount, CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.Refused);
    }

    /// <summary>Suspending an already suspended account is already in state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SuspendAccount_ReportsAlreadyInState_WhenTheAccountIsAlreadySuspendedAsync()
    {
        var provider = BuildProvider(Refused, SuspendedListing);

        var result = await provider.SuspendAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.AlreadyInState);
    }

    /// <summary>A refused suspend on an active account stays refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SuspendAccount_IsRefused_WhenTheAccountIsActiveAsync()
    {
        var provider = BuildProvider(Refused, ActiveListing);

        var result = await provider.SuspendAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.Refused);
    }

    /// <summary>Unsuspending an already active account is already in state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnsuspendAccount_ReportsAlreadyInState_WhenTheAccountIsAlreadyActiveAsync()
    {
        var provider = BuildProvider(Refused, ActiveListing);

        var result = await provider.UnsuspendAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.AlreadyInState);
    }

    /// <summary>A refused unsuspend on a suspended account stays refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnsuspendAccount_IsRefused_WhenTheAccountIsStillSuspendedAsync()
    {
        var provider = BuildProvider(Refused, SuspendedListing);

        var result = await provider.UnsuspendAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.Refused);
    }

    /// <summary>Terminating an account CWP no longer lists is already in state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateAccount_ReportsAlreadyInState_WhenTheAccountIsNotListedAsync()
    {
        var provider = BuildProvider(Refused, EmptyListing);

        var result = await provider.TerminateAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.AlreadyInState);
    }

    /// <summary>A failed listing is never read as "already gone".</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateAccount_IsRefused_WhenTheListingFailsAsync()
    {
        var provider = BuildProvider(Refused, Refused);

        var result = await provider.TerminateAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.Refused);
    }

    /// <summary>A removal CWP accepts is reported as succeeded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateAccount_Succeeds_WhenCwpAcceptsAsync()
    {
        var provider = BuildProvider(Ok);

        var result = await provider.TerminateAccountAsync("testuser", CancellationToken.None);

        result.Outcome.Should().Be(CwpAccountOutcome.Succeeded);
    }
}
