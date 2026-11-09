using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetDomainRenewalStates;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary><see cref="GetDomainRenewalStatesQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainRenewalStatesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registrar-linked domain answers its name, expiry and status; an unknown id is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_KnownAndUnknown_AnswersOnlyTheKnownStateAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var domain = await RenewalTestSeeds.SeedActiveDomainAsync(host, DateTimeOffset.UtcNow.AddDays(20), autoRenew: true);

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<DomainRenewalStateDto>>>(
            new GetDomainRenewalStatesQuery([domain.Id, int.MaxValue]));

        var state = result.Value.Should().ContainSingle().Subject;
        state.Id.Should().Be(domain.Id);
        state.Name.Should().Be(domain.Name);
        state.Status.Should().Be(DomainStatus.Active);
        state.IsAtRegistrar.Should().BeTrue();
        state.ExpiresAt.Should().BeCloseTo(domain.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }
}
