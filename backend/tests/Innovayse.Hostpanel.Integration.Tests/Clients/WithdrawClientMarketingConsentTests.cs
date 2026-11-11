using Innovayse.Hostpanel.Application.Clients.Commands.WithdrawClientMarketingConsent;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// Integration tests for the <c>WithdrawClientMarketingConsent</c> use case, driven through the bus the way the unsubscribe
/// use case does: the consent flag goes off, nothing else about the client changes, and a repeat is harmless.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class WithdrawClientMarketingConsentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sends the command for a client.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="clientId">The client to withdraw consent for.</param>
    /// <returns>The command's outcome.</returns>
    private static Task<Result> WithdrawAsync(IServiceProvider services, int clientId) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result>(new WithdrawClientMarketingConsentCommand(clientId), CancellationToken.None);

    /// <summary>An opted-in client is opted out, and the other settings stay as they were; doing it twice changes nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Withdraw_OptedInClient_TurnsOnlyTheConsentOff()
    {
        using var scope = factory.Services.CreateScope();
        var seeded = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);

        var first = await WithdrawAsync(scope.ServiceProvider, seeded.Client.Id);
        var second = await WithdrawAsync(scope.ServiceProvider, seeded.Client.Id);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        using var check = factory.Services.CreateScope();
        var client = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Clients.AsNoTracking().SingleAsync(c => c.Id == seeded.Client.Id);
        client.MarketingOptIn.Should().BeFalse();
        client.StatusUpdate.Should().BeTrue();
        client.AllowSso.Should().BeTrue();
    }

    /// <summary>An id that names no client is refused with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Withdraw_UnknownClient_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();

        var result = await WithdrawAsync(scope.ServiceProvider, 999_999);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
