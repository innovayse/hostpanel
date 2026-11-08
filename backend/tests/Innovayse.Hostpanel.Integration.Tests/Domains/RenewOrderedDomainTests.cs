using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Integration.Tests.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="RenewOrderedDomainCommand"/> through the real <see cref="IMessageBus"/>: the refusals
/// that keep a paid renewal line from extending a domain that is missing or not the payer's.
/// The registrar call itself is covered by <c>RenewDomainTests</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewOrderedDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A name that is not registered here is refused with <c>DOMAIN_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownDomain_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new RenewOrderedDomainCommand(1, $"missing-{Guid.NewGuid():N}.example", 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>Another client's domain is refused with the same code, so the answer never tells whose it is.</summary>
    [Fact]
    public async Task HandleAsync_AnotherClientsDomain_RefusesNotFoundAsync()
    {
        var name = $"theirs-{Guid.NewGuid():N}"[..18] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var holderId = await DomainsTestSeeds.SeedClientAsync(context);
            context.Domains.Add(RegisteredDomain.Register(
                holderId, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await InvokeAsync(new RenewOrderedDomainCommand(999_997, name, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>
    /// The payer's own domain is found whatever case the order line spells it in, so the renewal
    /// goes on to the registrar instead of being refused as missing.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OwnDomainInAnotherCase_IsNotRefusedAsMissingAsync()
    {
        int clientId;
        var name = $"mine-{Guid.NewGuid():N}"[..18] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            context.Domains.Add(RegisteredDomain.Register(
                clientId, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await InvokeAsync(new RenewOrderedDomainCommand(clientId, name.ToUpperInvariant(), 1));

        result.ErrorCode.Should().NotBe(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>
    /// A retry of an order line whose renewal already reached the registrar and was stored with the line is counted as
    /// renewed: the registrar is asked once, not twice, for a period the client paid for once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_TheSameOrderLineTwice_RenewsOnlyOnceAsync()
    {
        var (clientId, name) = await SeedActiveDomainAsync();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));
        var line = 424_201;

        var first = await InvokeAsync(new RenewOrderedDomainCommand(clientId, name, 1, line), host.Services);
        var second = await InvokeAsync(new RenewOrderedDomainCommand(clientId, name, 1, line), host.Services);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        TestRegistrar.RenewCallsFor(name).Should().Be(1);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Domains.AsNoTracking().SingleAsync(d => d.Name == name, CancellationToken.None))
            .LastRenewalOrderItemId.Should().Be(line);
    }

    /// <summary>A renewal bought on a different order line is a different period and is sent to the registrar.</summary>
    [Fact]
    public async Task HandleAsync_ADifferentOrderLine_RenewsAgainAsync()
    {
        var (clientId, name) = await SeedActiveDomainAsync();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));

        (await InvokeAsync(new RenewOrderedDomainCommand(clientId, name, 1, 424_202), host.Services)).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RenewOrderedDomainCommand(clientId, name, 1, 424_203), host.Services)).IsSuccess.Should().BeTrue();

        TestRegistrar.RenewCallsFor(name).Should().Be(2);
    }

    /// <summary>Seeds a client with an active, registrar-placed domain that can be renewed.</summary>
    /// <returns>The client and the domain's name.</returns>
    private async Task<(int ClientId, string Name)> SeedActiveDomainAsync()
    {
        var name = $"renew-{Guid.NewGuid():N}"[..18] + ".example";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        domain.Activate("REF-RENEW", DateTimeOffset.UtcNow.AddYears(1), DateTimeOffset.UtcNow);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        return (clientId, name);
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <param name="services">The host to dispatch on; the shared host when omitted.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(RenewOrderedDomainCommand cmd, IServiceProvider? services = null)
    {
        using var scope = (services ?? factory.Services).CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }
}
