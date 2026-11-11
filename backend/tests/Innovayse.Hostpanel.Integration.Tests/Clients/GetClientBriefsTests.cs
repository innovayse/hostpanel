using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientBriefs;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="GetClientBriefsQuery"/> through the real <see cref="IMessageBus"/>: the query other
/// modules dispatch to name or notify a client without reading the Clients tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientBriefsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known id comes back with its name and status; an unknown id is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownIds_ReturnsOnlyTheKnownClientAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory, "Grace", "Hopper");

        var result = await InvokeAsync(new GetClientBriefsQuery(ClientIds: [seeded.ClientId, int.MaxValue]));

        result.IsSuccess.Should().BeTrue();
        var brief = result.Value!.Should().ContainSingle().Subject;
        brief.Id.Should().Be(seeded.ClientId);
        brief.UserSubject.Should().Be(seeded.UserId);
        brief.FullName.Should().Be("Grace Hopper");
        brief.Status.Should().Be(ClientStatus.Active);
        brief.NotifyInvoice.Should().BeTrue("a new client receives invoice emails");
        brief.Settings.SeparateInvoices.Should().BeFalse("a new client gets one invoice per run");
        brief.CreditBalance.Should().Be(0m);
        brief.Email.Should().BeNull("the address was not asked for");
    }

    /// <summary>A record named by its owner's subject is found, and asking for the address resolves it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_BySubjectWithEmail_ResolvesTheOwnersAddressAsync()
    {
        var account = await ClientsTestSeeds.RegisterRealClientAsync(factory, "brief");

        var result = await InvokeAsync(new GetClientBriefsQuery(UserSubjects: [account.UserId], WithEmail: true));

        result.IsSuccess.Should().BeTrue();
        var brief = result.Value!.Should().ContainSingle().Subject;
        brief.UserSubject.Should().Be(account.UserId);
        brief.Email.Should().Be(account.Email);
    }

    /// <summary>A record named both by id and by subject is answered once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_SameRecordByIdAndSubject_ReturnsItOnceAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);

        var result = await InvokeAsync(new GetClientBriefsQuery([seeded.ClientId], [seeded.UserId]));

        result.Value!.Should().ContainSingle().Which.Id.Should().Be(seeded.ClientId);
    }

    /// <summary>No keys answer an empty list without touching the database.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoKeys_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new GetClientBriefsQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<ClientBriefDto>>> InvokeAsync(GetClientBriefsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<ClientBriefDto>>>(query);
    }
}
