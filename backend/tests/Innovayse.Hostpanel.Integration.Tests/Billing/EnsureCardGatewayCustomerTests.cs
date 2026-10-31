using Innovayse.Hostpanel.Application.Billing.Commands.EnsureCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="EnsureCardGatewayCustomerCommand"/> against Postgres with the card gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: a client gets one card gateway customer, created once and reused,
/// also when many requests ask at the same moment and when an earlier attempt died before storing it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EnsureCardGatewayCustomerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client without a customer gets one created and stored.</summary>
    [Fact]
    public async Task HandleAsync_NoCustomer_CreatesAndStoresOneAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);

        var result = await InvokeAsync(host, clientId);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        gateway.CustomersCreated.Should().Be(1);
        (await StoredAsync(host, clientId)).Should().Be(result.Value);
    }

    /// <summary>A client who has a customer keeps it and the gateway is not asked to create another.</summary>
    [Fact]
    public async Task HandleAsync_CustomerStored_ReusesItAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, "cus_already_1");

        var result = await InvokeAsync(host, clientId);

        result.Value.Should().Be("cus_already_1");
        gateway.CustomersCreated.Should().Be(0);
    }

    /// <summary>Many requests ask at once: one customer is created at the gateway and one is stored.</summary>
    [Fact]
    public async Task HandleAsync_ParallelRequests_CreateOneCustomerAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => InvokeAsync(host, clientId))));

        var stored = await StoredAsync(host, clientId);
        stored.Should().NotBeNull();
        results.Should().OnlyContain(r => r.IsSuccess && r.Value == stored);
        gateway.CustomersCreated.Should().Be(1);
    }

    /// <summary>
    /// A customer was opened for the client but never stored (the earlier attempt died in between): it is
    /// adopted, not duplicated.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrphanCustomerAtGateway_AdoptsItAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        gateway.OrphanCustomers[clientId] = "cus_orphan_1";

        var result = await InvokeAsync(host, clientId);

        result.Value.Should().Be("cus_orphan_1");
        gateway.CustomersCreated.Should().Be(0);
        (await StoredAsync(host, clientId)).Should().Be("cus_orphan_1");
    }

    /// <summary>
    /// The gateway created the customer but the answer was lost: the next try sends the same creation key, so
    /// the gateway hands back the same customer instead of a second one.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AnswerLost_RetryFindsTheSameCustomerAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        gateway.AfterCustomerCreated = _ => throw new HttpRequestException("The card gateway did not answer.");

        var first = async () => await InvokeAsync(host, clientId);
        await first.Should().ThrowAsync<HttpRequestException>();
        gateway.AfterCustomerCreated = null;
        var retry = await InvokeAsync(host, clientId);

        retry.IsSuccess.Should().BeTrue();
        gateway.CustomersCreated.Should().Be(1);
    }

    /// <summary>A gateway that refuses the creation leaves the client with no customer and the failure's error code.</summary>
    [Fact]
    public async Task HandleAsync_GatewayRefuses_StoresNothingAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = true, CustomerFailure = Application.Billing.Common.Enums.CardGatewayFailure.Unavailable };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);

        var result = await InvokeAsync(host, clientId);

        result.IsFailure.Should().BeTrue();
        (await StoredAsync(host, clientId)).Should().BeNull();
    }

    /// <summary>A deployment without card gateway keys answers <c>PAYMENT_METHOD_UNAVAILABLE</c> and creates nothing.</summary>
    [Fact]
    public async Task HandleAsync_GatewayNotConfigured_RefusesUnavailableAsync()
    {
        var gateway = new FakeCardPaymentGateway { IsConfigured = false };
        var host = BuildHost(gateway);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);

        var result = await InvokeAsync(host, clientId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        gateway.CustomersCreated.Should().Be(0);
    }

    /// <summary>Builds a host whose card gateway is <paramref name="gateway"/>.</summary>
    /// <param name="gateway">The fake card gateway to serve every call through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway gateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(gateway)));

    /// <summary>Reads the customer id stored on a client.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The stored customer id, or <see langword="null"/>.</returns>
    private static async Task<string?> StoredAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.CardGatewayCustomerId).SingleAsync();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client who needs a customer.</param>
    /// <returns>The handler's result.</returns>
    private static async Task<Result<string>> InvokeAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<string>>(new EnsureCardGatewayCustomerCommand(clientId));
    }
}
