using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One fulfilment test: its own plugin id, a <see cref="StubFulfilmentPlugin"/> enabled under it, a clock the
/// test moves by hand, and the host they run on. Only this plugin is resolved on the host, so the records
/// other tests left in the shared database name a plugin it does not resolve and are never touched.
/// </summary>
internal sealed class FulfilmentScenario : IDisposable
{
    /// <summary>The host running on <see cref="Clock"/> with the stub plugin enabled.</summary>
    private readonly WebApplicationFactory<Program> _host;

    /// <summary>Initializes a new instance of the <see cref="FulfilmentScenario"/> class.</summary>
    /// <param name="factory">The shared host the scenario host is derived from.</param>
    /// <param name="settings">Extra <c>ScheduledJobs</c> settings, by key under that section.</param>
    public FulfilmentScenario(IntegrationTestFactory factory, IReadOnlyDictionary<string, string>? settings = null)
    {
        PluginId = $"stub-{Guid.NewGuid():N}";
        Plugin = new StubFulfilmentPlugin();
        Clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var resolver = new FakeFulfilmentPluginResolver();
        resolver.Register(PluginId, Plugin);
        _host = factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting($"ScheduledJobs:{key}", value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFulfilmentPluginResolver>();
                services.AddSingleton<IFulfilmentPluginResolver>(resolver);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        });
    }

    /// <summary>Gets this scenario own plugin id.</summary>
    public string PluginId { get; }

    /// <summary>Gets the stub plugin enabled under <see cref="PluginId"/>.</summary>
    public StubFulfilmentPlugin Plugin { get; }

    /// <summary>Gets the clock the host reads.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>Gets the services of the scenario host.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>Runs one pass of the dispatch in a scope of its own.</summary>
    /// <returns>A task that completes when the pass ended.</returns>
    public async Task RunPassAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FulfilmentDispatcher>().RunAsync(CancellationToken.None);
    }

    /// <summary>Seeds an invoice that went through paid, refunded and lapsed.</summary>
    /// <returns>The saved invoice id.</returns>
    public async Task<int> SeedInvoiceAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await FulfilmentInvoiceSeeds.SeedAsync(db, Clock.GetUtcNow())).Id;
    }

    /// <summary>Seeds records of this scenario plugin for an invoice, all due now.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="kinds">The kinds to write a record for, in the order they are written.</param>
    /// <returns>A task that completes when the records are saved.</returns>
    public async Task SeedRecordsAsync(int invoiceId, params FulfilmentKind[] kinds)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var kind in kinds)
        {
            db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoiceId, PluginId, kind, Clock.GetUtcNow()));
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads a record of this scenario plugin as the database holds it.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="kind">The kind of the record.</param>
    /// <returns>The record, untracked.</returns>
    public async Task<FulfilmentRecord> ReadAsync(int invoiceId, FulfilmentKind kind)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().FulfilmentRecords
            .AsNoTracking()
            .SingleAsync(r => r.InvoiceId == invoiceId && r.PluginId == PluginId && r.Kind == kind, CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose() => _host.Dispose();
}
