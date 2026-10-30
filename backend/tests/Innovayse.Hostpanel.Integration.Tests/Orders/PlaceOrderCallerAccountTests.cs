using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> for the users of a client account: a sub-user and an identity plugin member are
/// resolved through the caller client resolver like the owner, need the ordering permission, and a plugin
/// member with no client yet gets one created from the plugin account on the first order and reuses it on
/// the next.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderCallerAccountTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The system name the stub plugin links its accounts under.</summary>
    private const string Provider = "crm";

    /// <summary>Builds a host whose identity plugin is <paramref name="plugin"/>.</summary>
    /// <param name="plugin">The enabled plugin.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(IClientResolver plugin) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));

    /// <summary>Builds the grant a stub plugin gives a member of <paramref name="accountId"/>.</summary>
    /// <param name="accountId">The account id in the plugin system.</param>
    /// <param name="canPurchase">Whether the member may purchase.</param>
    /// <returns>The plugin answer.</returns>
    private static ClientAccess Grant(string accountId, bool canPurchase) =>
        new(new ExternalAccount(Provider, accountId, null, null), canPurchase, CanManagePaymentMethods: false);

    /// <summary>Posts a one line order paid by bank transfer.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="productId">The product to order.</param>
    /// <returns>The answer.</returns>
    private static Task<HttpResponseMessage> OrderAsync(HttpClient http, int productId) =>
        http.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });

    /// <summary>Reads the client an order was placed for.</summary>
    /// <param name="response">The successful placement answer.</param>
    /// <returns>The client id stored on the order.</returns>
    private async Task<int> ClientOfOrderAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        return await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.ClientId).SingleAsync();
    }

    /// <summary>Counts the clients linked to a plugin account.</summary>
    /// <param name="accountId">The account id in the plugin system.</param>
    /// <returns>How many client rows carry the account.</returns>
    private async Task<int> ClientsOfAccountAsync(string accountId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        return await db.Clients.AsNoTracking()
            .CountAsync(c => c.ExternalAccountProvider == Provider && c.ExternalAccountId == accountId);
    }

    /// <summary>Links an existing client to a plugin account.</summary>
    /// <param name="clientId">The client to link.</param>
    /// <param name="accountId">The account id in the plugin system.</param>
    /// <returns>A task that completes once the link is saved.</returns>
    private async Task LinkClientToAccountAsync(int clientId, string accountId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.SetExternalAccount(Provider, accountId);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>A sub-user with the ordering permission orders on the account they are linked to, not on a new one.</summary>
    [Fact]
    public async Task SubUserWithThePermission_OrdersOnTheLinkedAccountAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var sub = await ClientsTestSeeds.LinkedToAsync(factory, "order-sub", seeded.ClientId, ClientPermission.PlaceNewOrders);

        var response = await OrderAsync(sub, productId);

        (await ClientOfOrderAsync(response)).Should().Be(seeded.ClientId);
    }

    /// <summary>A sub-user without the ordering permission is refused, and nothing is created for them.</summary>
    [Fact]
    public async Task SubUserWithoutThePermission_IsRefusedAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var sub = await ClientsTestSeeds.LinkedToAsync(factory, "order-sub-denied", seeded.ClientId, ClientPermission.ViewPayInvoices);

        var response = await OrderAsync(sub, productId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A plugin member who may purchase orders on the client linked to their account.</summary>
    [Fact]
    public async Task PluginMemberWhoMayPurchase_OrdersOnTheLinkedClientAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var accountId = $"acct-{Guid.NewGuid():N}";
        await LinkClientToAccountAsync(seeded.ClientId, accountId);
        var member = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "order-member");
        var plugin = new StubClientResolver();
        plugin.Grant(member.UserId, Grant(accountId, canPurchase: true));
        using var host = BuildHost(plugin);
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = member.Client.DefaultRequestHeaders.Authorization;

        var response = await OrderAsync(http, productId);

        (await ClientOfOrderAsync(response)).Should().Be(seeded.ClientId);
    }

    /// <summary>A plugin member who may only view is refused, and no client is created for their account.</summary>
    [Fact]
    public async Task PluginMemberWhoMayOnlyView_IsRefusedAndNoClientIsCreatedAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var accountId = $"acct-{Guid.NewGuid():N}";
        var member = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "order-viewer");
        var plugin = new StubClientResolver();
        plugin.Grant(member.UserId, Grant(accountId, canPurchase: false));
        using var host = BuildHost(plugin);
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = member.Client.DefaultRequestHeaders.Authorization;

        var response = await OrderAsync(http, productId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        (await ClientsOfAccountAsync(accountId)).Should().Be(0);
    }

    /// <summary>
    /// A plugin member with no client yet gets one linked to their plugin account on the first order, the same
    /// client takes the second order, and another member of the account lands on it too.
    /// </summary>
    [Fact]
    public async Task PluginMemberWithoutAClient_FirstOrderCreatesItAndLaterOrdersReuseItAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var accountId = $"acct-{Guid.NewGuid():N}";
        var first = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "order-first");
        var second = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "order-second");
        var plugin = new StubClientResolver();
        plugin.Grant(first.UserId, Grant(accountId, canPurchase: true));
        plugin.Grant(second.UserId, Grant(accountId, canPurchase: true));
        using var host = BuildHost(plugin);
        using var firstHttp = host.CreateClient();
        firstHttp.DefaultRequestHeaders.Authorization = first.Client.DefaultRequestHeaders.Authorization;
        using var secondHttp = host.CreateClient();
        secondHttp.DefaultRequestHeaders.Authorization = second.Client.DefaultRequestHeaders.Authorization;

        var firstClient = await ClientOfOrderAsync(await OrderAsync(firstHttp, productId));
        var repeatClient = await ClientOfOrderAsync(await OrderAsync(firstHttp, productId));
        var otherMemberClient = await ClientOfOrderAsync(await OrderAsync(secondHttp, productId));

        repeatClient.Should().Be(firstClient);
        otherMemberClient.Should().Be(firstClient);
        (await ClientsOfAccountAsync(accountId)).Should().Be(1);

        // The order made the caller a customer, so the portal routes now resolve to the new client.
        var services = await firstHttp.GetAsync("/api/me/services");
        services.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
