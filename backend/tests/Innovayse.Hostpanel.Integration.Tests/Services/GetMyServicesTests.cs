using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Hosting;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/me/services</c>, the client-facing read of the caller's own
/// services.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET /api/me/services without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task GetMyServicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin calling <c>GET /api/me/services</c> is refused with 403: the route requires the Client role.</summary>
    [Fact]
    public async Task GetMyServicesAsync_Returns403_AsAdminAsync()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A registered client reads their own services, empty or not, without a refusal.</summary>
    [Fact]
    public async Task GetMyServicesAsync_Returns200_AsClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services");

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Each service carries the day it was ordered, which the client area shows as its registration date.</summary>
    [Fact]
    public async Task GetMyServicesAsync_CarriesTheDateTheServiceWasOrderedAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-date");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    /// <summary>The client's own list names the product even when the product has no price in the client's currency.</summary>
    [Fact]
    public async Task GetMyServicesAsync_NamesAProductWithNoPriceInTheCurrencyAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-unpriced");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("productName").GetString().Should().Be("Starter");
    }

    /// <summary>A service of a digital product says so, and a hosting one does not, so the client area asks for setup only on the latter.</summary>
    [Fact]
    public async Task GetMyServicesAsync_FlagsAServiceOfADigitalProductAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-digital");
        var digitalId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId, productType: ProductType.Digital);
        var hostingId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        rows.Single(s => s.GetProperty("id").GetInt32() == digitalId).GetProperty("isDigital").GetBoolean().Should().BeTrue();
        rows.Single(s => s.GetProperty("id").GetInt32() == hostingId).GetProperty("isDigital").GetBoolean().Should().BeFalse();
    }

    /// <summary>A service carries its product's SKU, in the normalised form it is stored in, and null when the product has none.</summary>
    [Fact]
    public async Task GetMyServicesAsync_CarriesTheProductSkuOrNullAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-sku");
        var withSkuId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId, sku: $"sku-{Guid.NewGuid():N}");
        var withoutSkuId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        rows.Single(s => s.GetProperty("id").GetInt32() == withSkuId).GetProperty("sku").GetString().Should().StartWith("SKU-");
        rows.Single(s => s.GetProperty("id").GetInt32() == withoutSkuId).GetProperty("sku").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// A suspended service names why it is suspended, and carries the disk and bandwidth figures its
    /// server last reported for its domain.
    /// </summary>
    [Fact]
    public async Task GetMyServicesAsync_CarriesSuspensionSourceAndUsageAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-usage");
        var domain = $"site-{Guid.NewGuid():N}.test";
        var serviceId = await SeedSuspendedServiceWithUsageAsync(clientId, domain);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("suspensionSource").GetString().Should().Be(nameof(SuspensionSource.Overdue));
        var usage = row.GetProperty("usage");
        usage.GetProperty("diskUsedMb").GetInt64().Should().Be(512);
        usage.GetProperty("diskLimitMb").GetInt64().Should().Be(1024);
        usage.GetProperty("bandwidthUsedMb").GetInt64().Should().Be(2048);
        usage.GetProperty("bandwidthLimitMb").GetInt64().Should().Be(0);
    }

    /// <summary>
    /// A service on a server names the server for display but never carries its internal id.
    /// </summary>
    [Fact]
    public async Task GetMyServicesAsync_OmitsTheServerIdAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-no-server-id");
        var serviceId = await SeedSuspendedServiceWithUsageAsync(clientId, $"site-{Guid.NewGuid():N}.test");

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("server").GetProperty("name").ValueKind.Should().Be(JsonValueKind.String);
        row.TryGetProperty("serverId", out _).Should().BeFalse();
    }

    /// <summary>A service the server reported nothing for, and that is not suspended, carries neither field.</summary>
    [Fact]
    public async Task GetMyServicesAsync_LeavesSuspensionSourceAndUsageNullWhenAbsentAsync()
    {
        var (client, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-my-services-no-usage");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await client.GetAsync("/api/me/services");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("suspensionSource").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("usage").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// Seeds a server, a product and a service of the client on that server for the given domain,
    /// suspended for an overdue invoice, plus the usage the server reported for that domain.
    /// </summary>
    /// <param name="clientId">The owning client.</param>
    /// <param name="domain">The service's domain; the usage row is stored upper-cased to prove the match ignores case.</param>
    /// <returns>The new service's primary key.</returns>
    private async Task<int> SeedSuspendedServiceWithUsageAsync(int clientId, string domain)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var group = ProductGroup.Create($"my-services-usage-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, "Starter", null, null, null, "starter", ProductType.SharedHosting, 2m, 20m, DateTimeOffset.UtcNow);
        context.Products.Add(product);

        var server = Server.Create(new ServerDetails(
            Name: $"srv-{Guid.NewGuid():N}",
            Hostname: "srv.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.CPanel,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, null),
            UseSSL: true,
            MaxAccounts: null,
            IsDefault: false,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), DateTimeOffset.UtcNow);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var service = Domain.Services.ClientService.CreateOrdered(clientId, product.Id, BillingCycle.Monthly, domain, 2m, 2m, null, null, now);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "provisioning-ref-usage", now, server.Id);
        service.Suspend(now, hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        context.DiskUsageStats.Add(DiskUsageStat.Create(server.Name, domain.ToUpperInvariant(), "Owner", 512, 1024, 2048, 0, now));
        await context.SaveChangesAsync(CancellationToken.None);

        return service.Id;
    }
}
