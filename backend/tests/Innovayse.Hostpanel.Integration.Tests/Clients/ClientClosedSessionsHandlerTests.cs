using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>ClientClosedSessionsHandler</c>: closing an account ends every session its owner holds, through
/// every registered session revoker.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientClosedSessionsHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The closing revokes the owner's subject at every registered revoker.</summary>
    [Fact]
    public async Task ClientClosed_RevokesTheOwnersSessionsAtEveryRevokerAsync()
    {
        var panel = new RecordingSessionRevoker();
        var portal = new RecordingSessionRevoker();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionRevoker>();
            services.AddSingleton<ISessionRevoker>(panel);
            services.AddSingleton<ISessionRevoker>(portal);
        }));
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(1, "subject-closed", DateTimeOffset.UtcNow));

        panel.Revoked.Should().Equal("subject-closed");
        portal.Revoked.Should().Equal("subject-closed");
    }

    /// <summary>A closing with no owner subject revokes nothing and does not throw.</summary>
    [Fact]
    public async Task ClientClosed_WithoutAnOwnerSubject_RevokesNothingAsync()
    {
        var revoker = new RecordingSessionRevoker();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionRevoker>();
            services.AddSingleton<ISessionRevoker>(revoker);
        }));
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(1, string.Empty, DateTimeOffset.UtcNow));

        revoker.Revoked.Should().BeEmpty();
    }
}
