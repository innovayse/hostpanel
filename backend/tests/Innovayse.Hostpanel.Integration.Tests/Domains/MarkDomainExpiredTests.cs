using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.MarkDomainExpired;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>MarkDomainExpired</c> use case, an internal command
/// <c>CheckDomainExpiriesHandler</c> dispatches; there is no HTTP route for it, so it is
/// exercised here directly through the real bus (<c>IMessageBus.InvokeAsync</c>), the same way
/// the scheduled jobs are tested.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MarkDomainExpiredTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Marking an active domain expired transitions its status and persists the change.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkDomainExpired_ActiveDomain_TransitionsToExpiredAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var outcome = await bus.InvokeAsync<Result>(new MarkDomainExpiredCommand(domainId), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None);
        Assert.Equal(DomainStatus.Expired, reloaded.Status);
    }

    /// <summary>An unknown domain id answers a failed <see cref="Result"/> carrying <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkDomainExpired_UnknownDomain_FailsWithDomainNotFoundAsync()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new MarkDomainExpiredCommand(DomainsTestSeeds.UnknownId), CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(DomainsTestSeeds.DomainNotFoundCode, outcome.ErrorCode);
    }

    /// <summary>A non-positive domain id is refused by the validator before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MarkDomainExpired_NonPositiveId_IsRefusedByTheValidatorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await Assert.ThrowsAsync<ValidationException>(
            () => bus.InvokeAsync<Result>(new MarkDomainExpiredCommand(0), CancellationToken.None));
    }
}
