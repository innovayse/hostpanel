using Innovayse.Hostpanel.Application.Billing.Commands.EnsureGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="EnsureGatewayCustomerCommand"/> against Postgres with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: a client gets one account per gateway module, created once and reused,
/// also when many requests ask at the same moment, and the loser of the unique index reads the winner's account.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EnsureGatewayCustomerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="gateway">The test host.</param>
    /// <param name="clientId">The client who needs an account.</param>
    /// <param name="module">The gateway module.</param>
    /// <returns>The handler's result.</returns>
    private static async Task<Result<string>> InvokeAsync(StoredCredentialHost gateway, int clientId, string module = StoredCredentialHost.Module)
    {
        using var scope = gateway.Host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<string>>(new EnsureGatewayCustomerCommand(clientId, module));
    }

    /// <summary>A client without an account gets one created at the gateway and stored under the module.</summary>
    [Fact]
    public async Task HandleAsync_NoAccount_CreatesAndStoresOneAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);

        var result = await InvokeAsync(gateway, clientId);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        gateway.Plugin.CustomersCreated.Should().Be(1);
        var stored = (await gateway.CustomersOfAsync(clientId)).Should().ContainSingle().Subject;
        stored.GatewayModuleId.Should().Be(StoredCredentialHost.Module);
        stored.CustomerRef.Should().Be(result.Value);
    }

    /// <summary>A client who has an account at the module keeps it and the gateway is not asked to create another.</summary>
    [Fact]
    public async Task HandleAsync_AccountStored_ReusesItAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);
        await gateway.LinkAsync(clientId, "cus_already_1");

        var result = await InvokeAsync(gateway, clientId);

        result.Value.Should().Be("cus_already_1");
        gateway.Plugin.CustomersCreated.Should().Be(0);
    }

    /// <summary>Many requests ask at once: one account is created at the gateway and one row is stored.</summary>
    [Fact]
    public async Task HandleAsync_ParallelRequests_CreateOneAccountAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => InvokeAsync(gateway, clientId))));

        var stored = (await gateway.CustomersOfAsync(clientId)).Should().ContainSingle().Subject;
        results.Should().OnlyContain(r => r.IsSuccess && r.Value == stored.CustomerRef);
        gateway.Plugin.CustomersCreated.Should().Be(1);
    }

    /// <summary>
    /// A competing request stores its account while this one is still at the gateway: the unique index refuses the
    /// second row and this request answers the winner's account instead of failing or storing a duplicate.
    /// </summary>
    [Fact]
    public async Task HandleAsync_UniqueIndexLoser_ReadsTheWinnerAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);
        gateway.Plugin.AfterCustomerCreated = () => gateway.LinkAsync(clientId, "cus_winner_1");

        var result = await InvokeAsync(gateway, clientId);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        result.Value.Should().Be("cus_winner_1");
        (await gateway.CustomersOfAsync(clientId)).Should().ContainSingle().Which.CustomerRef.Should().Be("cus_winner_1");
    }

    /// <summary>A gateway that fails leaves the client with no account and the failure's error code.</summary>
    [Fact]
    public async Task HandleAsync_GatewayFails_StoresNothingAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.CustomerThrows = true;
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);

        var result = await InvokeAsync(gateway, clientId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await gateway.CustomersOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A module that is not offered answers <c>PAYMENT_METHOD_UNAVAILABLE</c> and creates nothing.</summary>
    [Fact]
    public async Task HandleAsync_ModuleNotOffered_RefusesUnavailableAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Resolver.Offered = false;
        var clientId = await BillingTestSeeds.SeedClientAsync(gateway.Host);

        var result = await InvokeAsync(gateway, clientId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        gateway.Plugin.CustomersCreated.Should().Be(0);
        (await gateway.CustomersOfAsync(clientId)).Should().BeEmpty();
    }
}
