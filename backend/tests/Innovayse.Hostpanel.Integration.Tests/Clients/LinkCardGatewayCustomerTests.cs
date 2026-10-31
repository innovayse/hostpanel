using Innovayse.Hostpanel.Application.Clients.Commands.LinkCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="LinkCardGatewayCustomerCommand"/> against Postgres: the customer is stored once, the first
/// stored one stands under a race, and one customer never belongs to two clients.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkCardGatewayCustomerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client with no customer gets the given one stored.</summary>
    [Fact]
    public async Task HandleAsync_ClientWithoutCustomer_StoresItAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var customer = NewCustomerId();

        var result = await InvokeAsync(new LinkCardGatewayCustomerCommand(clientId, customer));

        result.Value.Should().Be(customer);
        (await StoredAsync(clientId)).Should().Be(customer);
    }

    /// <summary>A client who already has a customer keeps it; the second id is not stored.</summary>
    [Fact]
    public async Task HandleAsync_ClientWithCustomer_KeepsTheStoredOneAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var first = NewCustomerId();
        await InvokeAsync(new LinkCardGatewayCustomerCommand(clientId, first));

        var result = await InvokeAsync(new LinkCardGatewayCustomerCommand(clientId, NewCustomerId()));

        result.Value.Should().Be(first);
        (await StoredAsync(clientId)).Should().Be(first);
    }

    /// <summary>
    /// Many requests store a different customer for one client at once: exactly one customer ends up stored
    /// and every request is told that same one.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ParallelRequests_StoreExactlyOneCustomerAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => InvokeAsync(new LinkCardGatewayCustomerCommand(clientId, NewCustomerId())))));

        var stored = await StoredAsync(clientId);
        stored.Should().NotBeNull();
        results.Should().OnlyContain(r => r.IsSuccess && r.Value == stored);
    }

    /// <summary>A customer that already belongs to another client is refused with <c>CARD_GATEWAY_CUSTOMER_ALREADY_LINKED</c>.</summary>
    [Fact]
    public async Task HandleAsync_CustomerOfAnotherClient_RefusesAlreadyLinkedAsync()
    {
        var owner = await BillingTestSeeds.SeedClientAsync(factory);
        var other = await BillingTestSeeds.SeedClientAsync(factory);
        var customer = NewCustomerId();
        await InvokeAsync(new LinkCardGatewayCustomerCommand(owner, customer));

        var result = await InvokeAsync(new LinkCardGatewayCustomerCommand(other, customer));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_GATEWAY_CUSTOMER_ALREADY_LINKED);
        (await StoredAsync(other)).Should().BeNull();
    }

    /// <summary>An id that names no client is <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownClient_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new LinkCardGatewayCustomerCommand(int.MaxValue, NewCustomerId()));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>A customer id unique to one test.</summary>
    /// <returns>A new <c>cus_</c> id.</returns>
    private static string NewCustomerId() => $"cus_{Guid.NewGuid():N}"[..24];

    /// <summary>Reads the customer id stored on a client.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The stored customer id, or <see langword="null"/>.</returns>
    private async Task<string?> StoredAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.CardGatewayCustomerId).SingleAsync();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<string>> InvokeAsync(LinkCardGatewayCustomerCommand cmd)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<string>>(cmd);
    }
}
