using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that makes an SSH key add lose the race for a service's last free key slot, every time: just before
/// the first save that adds a key to the service, it adds and commits another key on the same service through a separate
/// <see cref="AppDbContext"/> of its own scope, moving the service's SSH key revision as every add does. The loser already
/// counted a free slot, so only the service row's concurrency token can stop it. Test-only.
/// </summary>
internal sealed class SshKeyLimitRaceSaveInterceptor : SaveChangesInterceptor
{
    /// <summary>The service whose last slot is raced.</summary>
    private readonly int _serviceId;

    /// <summary>The derived host's services, set once the host exists, so the competitor has its own context and connection.</summary>
    private IServiceProvider? _services;

    /// <summary>Initializes the interceptor.</summary>
    /// <param name="serviceId">The service whose last slot is raced.</param>
    public SshKeyLimitRaceSaveInterceptor(int serviceId)
    {
        _serviceId = serviceId;
    }

    /// <summary>Gets whether the competing key was committed.</summary>
    public bool Fired { get; private set; }

    /// <summary>Hands the interceptor the services of the host it is registered in.</summary>
    /// <param name="services">The derived host's root services.</param>
    public void Attach(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Fired || _services is null || eventData.Context is not { } db)
        {
            return result;
        }

        var adding = db.ChangeTracker.Entries<SshKey>()
            .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.ClientServiceId == _serviceId);
        if (adding is null)
        {
            return result;
        }

        // Set before the competitor's own save, which passes through this interceptor too.
        Fired = true;
        using var scope = _services.CreateScope();
        var competitor = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await competitor.ClientServices.SingleAsync(s => s.Id == _serviceId, cancellationToken);
        service.RecordSshKeyAdded();
        competitor.SshKeys.Add(SshKey.Create(
            _serviceId, adding.Entity.ServerId, adding.Entity.AccountUsername, "competitor", SshTestSeeds.Key(250),
            "SHA256:competitor-" + Guid.NewGuid().ToString("N"), issuedToPluginId: null));
        await competitor.SaveChangesAsync(cancellationToken);
        return result;
    }
}
