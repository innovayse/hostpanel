using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="ICallerClientResolver"/> against Postgres: the owner, a sub-user and the members an identity plugin
/// vouches for resolve through the one port, and a plugin member who may view but not buy is refused the
/// ordering route with <c>CLIENT_PERMISSION_DENIED</c> like any user without the permission.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CallerClientResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The system name the stub plugin links its accounts under.</summary>
    private const string Provider = "crm";

    /// <summary>Builds a host whose identity plugin is <paramref name="plugin"/>, or none.</summary>
    /// <param name="plugin">The enabled plugin, or <see langword="null"/> for none.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(IClientResolver? plugin) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));

    /// <summary>Resolves the caller access as <paramref name="userId"/> through the host real wiring.</summary>
    /// <param name="host">The host to resolve in.</param>
    /// <param name="userId">The subject the request carries.</param>
    /// <returns>What the port answers.</returns>
    private static async Task<CallerClientAccess> ResolveAsAsync(WebApplicationFactory<Program> host, string userId)
    {
        using var scope = host.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));
        accessor.HttpContext = new DefaultHttpContext { User = principal };

        return await scope.ServiceProvider.GetRequiredService<ICallerClientResolver>().ResolveAsync(CancellationToken.None);
    }

    /// <summary>Builds the grant a stub plugin gives a member of <paramref name="accountId"/>.</summary>
    /// <param name="accountId">The account id in the plugin system.</param>
    /// <param name="canPurchase">Whether the member may purchase.</param>
    /// <param name="canManagePaymentMethods">Whether the member may manage payment methods.</param>
    /// <returns>The plugin answer.</returns>
    private static ClientAccess Grant(string accountId, bool canPurchase, bool canManagePaymentMethods) =>
        new(new ExternalAccount(Provider, accountId, null, null), canPurchase, canManagePaymentMethods);

    /// <summary>Seeds a client linked to an external account and returns its id.</summary>
    /// <param name="accountId">The account id in the plugin system.</param>
    /// <returns>The new client primary key.</returns>
    private async Task<int> SeedLinkedClientAsync(string accountId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Plug", "In", $"plug-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        client.SetExternalAccount(Provider, accountId);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        return client.Id;
    }

    /// <summary>With no plugin the owner of a client record acts for it with every permission.</summary>
    [Fact]
    public async Task WithoutAPlugin_TheOwnerActsForTheirClientWithEveryPermissionAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        using var host = BuildHost(plugin: null);

        var access = await ResolveAsAsync(host, seeded.UserId);

        access.Should().Be(new CallerClientAccess(seeded.ClientId, true, ClientPermission.All));
    }

    /// <summary>With no plugin a sub-user acts for the one account they are linked to, with what the link grants.</summary>
    [Fact]
    public async Task WithoutAPlugin_ASubUserActsForTheLinkedClientWithTheLinkedPermissionsAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var sub = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "resolver-sub");
        await ClientsTestSeeds.LinkUserAsync(factory, sub.Client, sub.Email, seeded.ClientId, ClientPermission.ViewDomains);
        using var host = BuildHost(plugin: null);

        var access = await ResolveAsAsync(host, sub.UserId);

        access.Should().Be(new CallerClientAccess(seeded.ClientId, false, ClientPermission.ViewDomains));
    }

    /// <summary>A plugin member who may purchase acts for the linked client and may order and pay, but is never the owner.</summary>
    [Fact]
    public async Task WithAPlugin_AMemberWhoMayPurchaseMayOrderAndPayButIsNotTheOwnerAsync()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        var clientId = await SeedLinkedClientAsync(accountId);
        var subject = $"member-{Guid.NewGuid():N}";
        var plugin = new StubClientResolver();
        plugin.Grant(subject, Grant(accountId, canPurchase: true, canManagePaymentMethods: false));
        using var host = BuildHost(plugin);

        var access = await ResolveAsAsync(host, subject);

        access.ClientId.Should().Be(clientId);
        access.IsOwner.Should().BeFalse();
        access.Permissions.Should().HaveFlag(ClientPermission.PlaceNewOrders).And.HaveFlag(ClientPermission.ViewPayInvoices);
    }

    /// <summary>A plugin member who may view but not buy holds no ordering, invoice or payment method permission.</summary>
    [Fact]
    public async Task WithAPlugin_AMemberWhoMayViewButNotBuyHoldsNoMoneyPermissionAsync()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        var clientId = await SeedLinkedClientAsync(accountId);
        var subject = $"viewer-{Guid.NewGuid():N}";
        var plugin = new StubClientResolver();
        plugin.Grant(subject, Grant(accountId, canPurchase: false, canManagePaymentMethods: false));
        using var host = BuildHost(plugin);

        var access = await ResolveAsAsync(host, subject);

        access.ClientId.Should().Be(clientId);
        access.Permissions.Should().HaveFlag(ClientPermission.ViewProductsServices);
        access.Permissions.Should().NotHaveFlag(ClientPermission.PlaceNewOrders).And.NotHaveFlag(ClientPermission.ViewPayInvoices);
    }

    /// <summary>A member who may only manage payment methods gets the one flag those actions are guarded by, and cannot order.</summary>
    [Fact]
    public async Task WithAPlugin_APaymentMethodManagerGetsThePaymentFlagButCannotOrderAsync()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        await SeedLinkedClientAsync(accountId);
        var subject = $"payer-{Guid.NewGuid():N}";
        var plugin = new StubClientResolver();
        plugin.Grant(subject, Grant(accountId, canPurchase: false, canManagePaymentMethods: true));
        using var host = BuildHost(plugin);

        var access = await ResolveAsAsync(host, subject);

        access.Permissions.Should().HaveFlag(ClientPermission.ViewPayInvoices).And.NotHaveFlag(ClientPermission.PlaceNewOrders);
    }

    /// <summary>An enabled plugin decides alone: a user it refuses acts for nothing even when they own a client record.</summary>
    [Fact]
    public async Task WithAPlugin_APrincipalItRefusesActsForNoAccountEvenIfTheyOwnOneAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        using var host = BuildHost(new StubClientResolver());

        var access = await ResolveAsAsync(host, seeded.UserId);

        access.Should().Be(CallerClientAccess.None);
    }

    /// <summary>An account no client is linked to yet keeps its mapped permissions and names no client.</summary>
    [Fact]
    public async Task WithAPlugin_AnAccountWithNoClientYetHasPermissionsButNoClientIdAsync()
    {
        var subject = $"first-order-{Guid.NewGuid():N}";
        var plugin = new StubClientResolver();
        plugin.Grant(subject, Grant($"new-{Guid.NewGuid():N}", canPurchase: true, canManagePaymentMethods: true));
        using var host = BuildHost(plugin);

        var access = await ResolveAsAsync(host, subject);

        access.ClientId.Should().BeNull();
        access.Permissions.Should().HaveFlag(ClientPermission.PlaceNewOrders);
    }

    /// <summary>Over HTTP a plugin member who may view but not buy reads their services and is refused an order.</summary>
    [Fact]
    public async Task WithAPlugin_AViewOnlyMemberReadsServicesAndIsRefusedAnOrderAsync()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        await SeedLinkedClientAsync(accountId);
        var signedIn = await ClientsTestSeeds.RegisterClientAsync(factory, "resolver-viewer");
        var plugin = new StubClientResolver();
        plugin.Grant(signedIn.UserId, Grant(accountId, canPurchase: false, canManagePaymentMethods: false));
        using var host = BuildHost(plugin);
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = signedIn.Client.DefaultRequestHeaders.Authorization;

        var read = await http.GetAsync("/api/me/services");
        var order = await http.PostAsJsonAsync("/api/me/services", new { productId = 1 });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        order.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(order, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }
}
