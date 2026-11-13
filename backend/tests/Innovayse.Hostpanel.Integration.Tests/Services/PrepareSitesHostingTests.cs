using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.PrepareSitesHosting;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// A provisioned service of an AI Hosting product (metadata <c>innovayseSites.prepare</c>) has its hosting account
/// prepared for Innovayse Sites deploys through the server's SSH key agent; any other product is left alone (#383).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PrepareSitesHostingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The provisioned event of an AI Hosting service prepares its account's domain once.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provisioned_AiHostingProduct_PreparesTheAccountsDomainAsync()
    {
        var (clientId, serverId, serviceId, domain) = await SeedAsync("sites-prepare-yes", """{"innovayseSites":{"prepare":true}}""");

        await factory.Services.PublishAndWaitAsync(new ServiceProvisionedEvent(serviceId, clientId, "ref-1", TestMoments.Seeded));

        SshTestSeeds.Agents(factory).AgentFor(serverId).PreparedSites.Should().ContainSingle()
            .Which.Should().Be(("web", domain));
    }

    /// <summary>A product without the flag (or with it false) never reaches the agent.</summary>
    /// <param name="metadata">The product metadata.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("""{"credits":500}""")]
    [InlineData("""{"innovayseSites":{"prepare":false}}""")]
    public async Task Provisioned_OtherProduct_PreparesNothingAsync(string? metadata)
    {
        var (clientId, serverId, serviceId, _) = await SeedAsync($"sites-no-{Guid.NewGuid():N}"[..24], metadata);

        await factory.Services.PublishAndWaitAsync(new ServiceProvisionedEvent(serviceId, clientId, "ref-1", TestMoments.Seeded));

        SshTestSeeds.Agents(factory).AgentFor(serverId).PreparedSites.Should().BeEmpty();
    }

    /// <summary>A refusal from the server is reported as a failure of the command, never thrown into the event chain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Prepare_ServerRefuses_FailsTheCommandAsync()
    {
        var (_, serverId, serviceId, _) = await SeedAsync("sites-prepare-refused", """{"innovayseSites":{"prepare":true}}""");
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        agent.PrepareSiteFailure = SshProviderFailure.SiteNotOwned;

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new PrepareSitesHostingCommand(serviceId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        agent.PreparedSites.Should().BeEmpty();
    }

    /// <summary>Seeds an owner, an SSH-enabled server and an active hosting service (account <c>web</c>, its own domain) of a product with <paramref name="metadata"/>.</summary>
    /// <param name="label">Unique label.</param>
    /// <param name="metadata">The product metadata JSON, or null.</param>
    /// <returns>The client, server and service ids and the service's domain.</returns>
    private async Task<(int ClientId, int ServerId, int ServiceId, string Domain)> SeedAsync(string label, string? metadata)
    {
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, label);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{label}.example.test", 22);
        var domain = $"{label}.sites.example.test";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            var group = ProductGroup.Create($"sites-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            var product = Product.Create(group.Id, "AI Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetMetadata(metadata);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
            var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
            service.AssignHostingAccount(domain, "web", "Secret-12345");
            await ServicesTestSeeds.AddActivatedAsync(context, service, "web", DateTimeOffset.UtcNow, serverId);
            await context.SaveChangesAsync(CancellationToken.None);
            return (clientId, serverId, service.Id, domain);
        }
    }
}
