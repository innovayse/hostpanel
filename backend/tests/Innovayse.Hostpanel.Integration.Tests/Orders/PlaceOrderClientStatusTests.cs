using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> for a signed-in client whose account status forbids ordering: a suspended
/// or closed account is refused with 403 before anything is priced or created.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderClientStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A suspended or closed account cannot place an order; the refusal names the status.</summary>
    /// <param name="status">The status the account is in.</param>
    /// <param name="expectedCode">The code the refusal carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ClientStatus.Suspended, HostpanelErrorCodes.CLIENT_SUSPENDED)]
    [InlineData(ClientStatus.Closed, HostpanelErrorCodes.CLIENT_CLOSED)]
    public async Task PlaceOrder_BySuspendedOrClosedAccount_IsRefusedAsync(ClientStatus status, string expectedCode)
    {
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, $"place-status-{status}");
        await ClientStatusSeeds.SetAsync(factory, clientId, status);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, expectedCode);
    }
}
