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

    /// <summary>
    /// The resolver the host answers with; a test may enable further plugins on it with <see cref="AddPlugin"/>.
    /// </summary>
    private readonly FakeFulfilmentPluginResolver _resolver;

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
        _resolver = resolver;
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

    /// <summary>Gets the host the scenario runs on, for a test that calls its routes.</summary>
    public WebApplicationFactory<Program> Host => _host;

    /// <summary>
    /// Enables one more stub plugin on the scenario host, under an id of its own. The resolver is read on every
    /// pass, so a plugin added after the host started is resolved from the next pass on.
    /// </summary>
    /// <returns>The new plugin id and the stub enabled under it.</returns>
    public EnabledStubPlugin AddPlugin()
    {
        var added = new EnabledStubPlugin($"stub-{Guid.NewGuid():N}", new StubFulfilmentPlugin());
        _resolver.Register(added.PluginId, added.Plugin);
        return added;
    }

    /// <summary>
    /// The reference the host computes for <paramref name="pluginId"/> and <paramref name="clientId"/>: the
    /// one source an endpoint call of that plugin takes its <c>PluginCaller.ClientRef</c> from.
    /// </summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientId">The host's client id.</param>
    /// <returns>The plugin's reference to the client.</returns>
    public string ClientRefOf(string pluginId, int clientId)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPluginReferences>().ForClient(pluginId, clientId);
    }

    /// <summary>
    /// Seeds a record of this scenario plugin that failed every attempt, the way the dispatcher leaves it:
    /// claimed and failed up to the attempt limit, each a day after the last so every back-off is over.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="kind">The kind of the record.</param>
    /// <param name="error">The error the last attempt failed with.</param>
    /// <returns>The saved record id.</returns>
    public async Task<int> SeedFailedRecordAsync(int invoiceId, FulfilmentKind kind, string error)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Clock.GetUtcNow();
        var record = FulfilmentRecord.Create(invoiceId, PluginId, kind, now);
        for (var attempt = 0; attempt < FulfilmentRecord.MaxAttempts; attempt++)
        {
            now = now.AddDays(1);
            record.Claim(now, TimeSpan.FromMinutes(5)).Should().BeTrue();
            record.MarkFailed(error, now);
        }

        db.FulfilmentRecords.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);
        return record.Id;
    }

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
    public Task SeedRecordsAsync(int invoiceId, params FulfilmentKind[] kinds) =>
        SeedRecordsForAsync(PluginId, invoiceId, kinds);

    /// <summary>Seeds records of a plugin enabled on this scenario for an invoice, all due now.</summary>
    /// <param name="pluginId">The plugin the records name: <see cref="PluginId"/> or one from <see cref="AddPlugin"/>.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="kinds">The kinds to write a record for, in the order they are written.</param>
    /// <returns>A task that completes when the records are saved.</returns>
    public async Task SeedRecordsForAsync(string pluginId, int invoiceId, params FulfilmentKind[] kinds)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var kind in kinds)
        {
            db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoiceId, pluginId, kind, Clock.GetUtcNow()));
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
