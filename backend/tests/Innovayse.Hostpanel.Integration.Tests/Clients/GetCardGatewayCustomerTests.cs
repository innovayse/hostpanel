using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="GetCardGatewayCustomerQuery"/> through the real <see cref="IMessageBus"/>: it tells the Billing
/// module which card gateway customer a client has, without Billing reading the Clients table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCardGatewayCustomerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client who never saved a card has no customer.</summary>
    [Fact]
    public async Task HandleAsync_ClientWithoutCustomer_AnswersNullAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var result = await InvokeAsync(new GetCardGatewayCustomerQuery(clientId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ClientId.Should().Be(clientId);
        result.Value.CustomerId.Should().BeNull();
    }

    /// <summary>A client with a customer on file gets it back.</summary>
    [Fact]
    public async Task HandleAsync_ClientWithCustomer_AnswersItAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.StampCardCustomerAsync(factory, clientId, $"cus_get_{Guid.NewGuid():N}"[..30]);

        var result = await InvokeAsync(new GetCardGatewayCustomerQuery(clientId));

        result.Value!.CustomerId.Should().StartWith("cus_get_");
    }

    /// <summary>An id that names no client is <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownClient_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new GetCardGatewayCustomerQuery(int.MaxValue));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<CardGatewayCustomerDto>> InvokeAsync(GetCardGatewayCustomerQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<CardGatewayCustomerDto>>(query);
    }
}
