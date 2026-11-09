using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>POST /api/me/services</c> for a client whose account status forbids ordering: a suspended
/// or closed account is refused with 403 before any product is looked at, an inactive one orders as usual.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderMyServiceClientStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A suspended or closed account cannot order; the refusal names the status.</summary>
    /// <param name="status">The status the account is in.</param>
    /// <param name="expectedCode">The code the refusal carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ClientStatus.Suspended, HostpanelErrorCodes.CLIENT_SUSPENDED)]
    [InlineData(ClientStatus.Closed, HostpanelErrorCodes.CLIENT_CLOSED)]
    public async Task Order_ByASuspendedOrClosedAccount_IsRefusedAsync(ClientStatus status, string expectedCode)
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, $"order-status-{status}");
        await ClientStatusSeeds.SetAsync(factory, clientId, status);

        var response = await client.PostAsJsonAsync("/api/me/services", new
        {
            productId = 1,
            billingCycle = nameof(BillingCycle.Monthly),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, expectedCode);
    }

    /// <summary>An inactive account is not refused by the status guard: the order reaches the product lookup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Order_ByAnInactiveAccount_ReachesTheProductLookupAsync()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "order-status-inactive");
        await ClientStatusSeeds.SetAsync(factory, clientId, ClientStatus.Inactive);

        var response = await client.PostAsJsonAsync("/api/me/services", new
        {
            productId = 999999,
            billingCycle = nameof(BillingCycle.Monthly),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }
}
