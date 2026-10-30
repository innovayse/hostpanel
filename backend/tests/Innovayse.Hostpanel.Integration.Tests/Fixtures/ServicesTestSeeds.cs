using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and assertion helpers shared by every <c>Services/&lt;UseCase&gt;Tests.cs</c> class, so
/// each per-use-case file states only what makes it different rather than re-deriving an admin
/// token or a client/product pair.
/// </summary>
public static class ServicesTestSeeds
{
    /// <summary>Shared test password for every user this class registers.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>An id no seeded or created service ever takes.</summary>
    public const int NoSuchServiceId = 999_999_999;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client with the Admin role.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Reads the <c>type</c> field of a problem+json refusal.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The error code.</returns>
    public static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("type").GetString();
    }

    /// <summary>Seeds a client and an active hosting product, priced only in USD.</summary>
    /// <param name="scope">The DI scope to seed through.</param>
    /// <param name="monthlyPrice">The product's monthly USD price, or <see langword="null"/> for none.</param>
    /// <returns>The client id and product id.</returns>
    public static async Task<ClientProductIds> SeedClientAndProductAsync(
        IServiceScope scope, decimal? monthlyPrice)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);

        var group = ProductGroup.Create($"services-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, "starter", ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        if (monthlyPrice is { } price)
        {
            product.SetPrice("USD", BillingCycle.Monthly, price);
        }

        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        return new ClientProductIds(client.Id, product.Id);
    }

    /// <summary>
    /// Registers a new client user, waits for Wolverine to create the corresponding
    /// <c>Client</c> row, and returns an authenticated <see cref="HttpClient"/> alongside its
    /// numeric client id.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">A short, unique-per-test label folded into the registered email.</param>
    /// <returns>The authenticated client and its numeric client id.</returns>
    public static async Task<SignedInClient> RegisterOwnerAsync(
        IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Service",
            lastName = "Owner"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientId = 0;
        for (var i = 0; i < 10 && clientId == 0; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/clients/me");
            if (meResponse.IsSuccessStatusCode)
            {
                var json = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientId = idProp.GetInt32();
                }
            }
        }

        clientId.Should().BeGreaterThan(0, "Wolverine should have created the Client record");
        return new SignedInClient(httpClient, clientId, userId, email);
    }

    /// <summary>
    /// Registers a second client user who owns nothing: the "somebody else" of every ownership
    /// test.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client for the stranger.</returns>
    public static async Task<HttpClient> RegisterStrangerAsync(IntegrationTestFactory factory)
    {
        var httpClient = factory.CreateClient();
        var email = $"my-service-stranger-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Service",
            lastName = "Stranger"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Creates a Pending service directly against the database, owned by the given client.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The owning client's primary key.</param>
    /// <param name="domain">The domain the service carries, or <see langword="null"/> for none.</param>
    /// <param name="productType">The type of the product the service orders; a hosting product unless a test needs another.</param>
    /// <returns>The new service's primary key.</returns>
    public static async Task<int> SeedPendingServiceAsync(
        IntegrationTestFactory factory, int clientId, string? domain = null, ProductType productType = ProductType.SharedHosting)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // The client row already exists (created by Wolverine on registration); only the
        // product and the service are seeded here.
        var group = ProductGroup.Create($"my-services-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, "starter", productType, 2m, 20m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = domain is null
            ? Domain.Services.ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow)
            : Domain.Services.ClientService.CreateOrdered(
                clientId, product.Id, BillingCycle.Monthly, domain, 0m, 0m, null, null, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        return service.Id;
    }

    /// <summary>
    /// Seeds a client, a product and one service of theirs in the given status. The transitions are
    /// recorded as already done on the hosting panel, so no panel call is asked for.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="status">The status the service is left in.</param>
    /// <returns>The new service's primary key.</returns>
    public static async Task<int> SeedServiceAsync(IntegrationTestFactory factory, ServiceStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var service = Domain.Services.ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        if (status is ServiceStatus.Pending)
        {
            context.ClientServices.Add(service);
        }
        else
        {
            await AddActivatedAsync(context, service, "acct1", DateTimeOffset.UtcNow);
        }

        if (status is ServiceStatus.Suspended)
        {
            service.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
        }

        if (status is ServiceStatus.Terminated)
        {
            service.Terminate(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return service.Id;
    }

    /// <summary>
    /// Adds a new service, saves it, then activates it, the order provisioning follows: the
    /// activation's event names the service by its id, which only a saved service has. The
    /// activation itself is left unsaved, so the caller can change more on the service and save once.
    /// </summary>
    /// <param name="context">The data context to seed through; the service stays tracked by it.</param>
    /// <param name="service">The new, pending service.</param>
    /// <param name="provisioningRef">The hosting panel's reference for the account.</param>
    /// <param name="activatedAt">When the service went live, also the start of its first billing period.</param>
    /// <param name="serverId">The server the account lives on, or <see langword="null"/> when none is recorded.</param>
    /// <returns>A task that completes once the service is saved and activated.</returns>
    public static async Task AddActivatedAsync(
        IHostpanelDbContext context,
        Domain.Services.ClientService service,
        string provisioningRef,
        DateTimeOffset activatedAt,
        int? serverId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);

        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        if (serverId is { } server)
        {
            service.ActivateOn(server, provisioningRef, activatedAt, activatedAt);
        }
        else
        {
            service.Activate(provisioningRef, activatedAt, activatedAt);
        }
    }
}
