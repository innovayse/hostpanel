using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.LinkDomainService;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="LinkDomainServiceCommand"/> through the real <see cref="IMessageBus"/>: linking a
/// domain to the hosting service bought with it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkDomainServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The domain records the linked service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ExistingDomain_LinksTheServiceAsync()
    {
        int domainId;
        int clientId;
        int serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            serviceId = await DomainsTestSeeds.SeedServiceAsync(context, clientId);
            var domain = RegisteredDomain.Register(
                clientId, $"link-{Guid.NewGuid():N}"[..17] + ".example", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
            domainId = domain.Id;
        }

        var result = await InvokeAsync(new LinkDomainServiceCommand(domainId, serviceId));

        result.IsSuccess.Should().BeTrue();
        using var check = factory.Services.CreateScope();
        var reread = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Domains.AsNoTracking().SingleAsync(d => d.Id == domainId);
        reread.LinkedServiceId.Should().Be(serviceId);
    }

    /// <summary>A service id that names no service is refused with <c>SERVICE_NOT_FOUND</c> and nothing is linked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownService_RefusesNotFoundAsync()
    {
        int domainId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var domain = RegisteredDomain.Register(
                clientId, $"link-{Guid.NewGuid():N}"[..17] + ".example", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
            domainId = domain.Id;
        }

        var result = await InvokeAsync(new LinkDomainServiceCommand(domainId, int.MaxValue));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        using var check = factory.Services.CreateScope();
        var reread = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Domains.AsNoTracking().SingleAsync(d => d.Id == domainId);
        reread.LinkedServiceId.Should().BeNull();
    }

    /// <summary>An id that names no domain is refused with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownDomain_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new LinkDomainServiceCommand(int.MaxValue, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>A cancelled domain is no longer managed here, so linking is refused with <c>DOMAIN_LINK_NOT_ALLOWED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_CancelledDomain_RefusesLinkNotAllowedAsync()
    {
        int domainId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var domain = RegisteredDomain.Register(
                clientId, $"link-{Guid.NewGuid():N}"[..17] + ".example", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            domain.Cancel();
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
            domainId = domain.Id;
        }

        var result = await InvokeAsync(new LinkDomainServiceCommand(domainId, 4242));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_LINK_NOT_ALLOWED);
        using var check = factory.Services.CreateScope();
        var reread = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Domains.AsNoTracking().SingleAsync(d => d.Id == domainId);
        reread.LinkedServiceId.Should().BeNull();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(LinkDomainServiceCommand cmd)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }
}
