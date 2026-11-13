using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Seeds servers with SSH settings and services assigned to them, for the SSH key use cases.</summary>
public static class SshTestSeeds
{
    /// <summary>The fake agents behind every server in the shared host.</summary>
    /// <param name="factory">The shared host.</param>
    /// <returns>The fake factory the host resolves.</returns>
    public static FakeSshKeyProviderFactory Agents(IntegrationTestFactory factory) =>
        (FakeSshKeyProviderFactory)factory.Services.GetRequiredService<ISshKeyProviderFactory>();

    /// <summary>A syntactically valid ED25519 public key line whose 32 key bytes are all <paramref name="seed"/>.</summary>
    /// <param name="seed">Byte value.</param>
    /// <returns>The key line, with a comment.</returns>
    public static string Key(byte seed)
    {
        var blob = new byte[51];
        blob[3] = 11;
        "ssh-ed25519"u8.CopyTo(blob.AsSpan(4));
        blob[18] = 32;
        blob.AsSpan(19).Fill(seed);
        return "ssh-ed25519 " + Convert.ToBase64String(blob) + " me@laptop";
    }

    /// <summary>Seeds a server with SSH settings.</summary>
    /// <param name="factory">The shared host.</param>
    /// <param name="hostname">Customer-facing hostname.</param>
    /// <param name="port">Customer-facing port.</param>
    /// <param name="module">Control panel module.</param>
    /// <param name="enabled">Whether SSH key management is enabled.</param>
    /// <returns>The server id.</returns>
    public static async Task<int> SeedServerAsync(
        IntegrationTestFactory factory, string hostname, int port, ServerModule module = ServerModule.Cwp7, bool enabled = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = Server.Create(new ServerDetails(
            $"ssh-srv-{Guid.NewGuid():N}", hostname, null, null, module, "root", new ServerCredentials(null, null, "hash"), true, null,
            false, false, 0m, null, null, ServerNameServers.None), TestMoments.Seeded);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);

        var settings = ServerSshSettings.CreateFor(server.Id);
        settings.Configure(enabled, hostname, port, hostname, 22, "hpsshkeys", "PRIVATE-KEY-PLACEHOLDER",
            "SHA256:" + new string('A', 43), 5, 5);
        context.ServerSshSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken.None);
        return server.Id;
    }

    /// <summary>Seeds an active hosting service for a client on a server.</summary>
    /// <param name="factory">The shared host.</param>
    /// <param name="clientId">Owning client.</param>
    /// <param name="serverId">Assigned server.</param>
    /// <param name="username">Hosting account name.</param>
    /// <returns>The service id.</returns>
    public static async Task<int> SeedServiceAsync(IntegrationTestFactory factory, int clientId, int serverId, string username)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ProductGroup.Create($"ssh-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, username, DateTimeOffset.UtcNow, serverId);
        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }

    /// <summary>Registers a client with one active service on a fresh SSH-enabled server.</summary>
    /// <param name="factory">The shared host.</param>
    /// <param name="label">Label for the generated e-mail.</param>
    /// <returns>The client's HTTP client, its id, the server id and the service id.</returns>
    public static async Task<SshOwnerScenario> OwnerWithServiceAsync(
        IntegrationTestFactory factory, string label)
    {
        var (http, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, label);
        var serverId = await SeedServerAsync(factory, $"{label}.example.test", 22);
        var serviceId = await SeedServiceAsync(factory, clientId, serverId, "web");
        return new SshOwnerScenario(http, clientId, serverId, serviceId);
    }

    /// <summary>
    /// Changes a service through its own domain method and saves it with domain events suppressed, so
    /// no handler reacts and the test can dispatch the one command it is about.
    /// </summary>
    /// <param name="factory">The shared host.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="change">The domain method to call, for example a suspension.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    public static async Task ChangeServiceQuietlyAsync(IntegrationTestFactory factory, int serviceId, Action<ClientService> change)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            var service = await context.ClientServices.FindAsync([serviceId], CancellationToken.None);
            change(service!);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Adds a key through the API and returns its id.</summary>
    /// <param name="http">The owner's client.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="seed">Key seed.</param>
    /// <returns>The key id.</returns>
    public static async Task<Guid> AddKeyAsync(HttpClient http, int serviceId, byte seed)
    {
        var response = await http.PostAsJsonAsync($"/api/me/services/{serviceId}/ssh/keys", new { label = "laptop", publicKey = Key(seed) });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("keyId").GetGuid();
    }
}
