using Innovayse.Hostpanel.Application.Admin.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Support.Seeding;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the public demo's sample data and wipes it on a schedule. Seeding runs the per-concern
/// seeders in dependency order; resetting truncates every table in the model except the identity
/// roles and the migration-seeded reference tables, then seeds again, all inside one transaction so a failure leaves yesterday's data
/// standing instead of an empty database.
/// </summary>
public sealed class DemoDataManager : IDemoDataManager
{
    /// <summary>Identity role tables kept across a reset (roles are configuration, not demo data) and the recurring jobs' run records (the reset runs under its own lease, and the record of when each job last succeeded is not demo data). Tables seeded by migrations (<c>HasData</c>) are kept too, derived from the model.</summary>
    private static readonly HashSet<string> KeptTables = ["AspNetRoles", "AspNetRoleClaims", "JobRuns"];

    /// <summary>The data context the seeders write through and the reset truncates.</summary>
    private readonly AppDbContext _db;

    /// <summary>Creates the demo accounts.</summary>
    private readonly UserManager<AppUser> _users;

    /// <summary>Discards the domain events the seeded rows would raise.</summary>
    private readonly IDomainEventGate _eventGate;

    /// <summary>The demo switch and the published accounts.</summary>
    private readonly IOptions<DemoOptions> _options;

    /// <summary>The configured contact addresses of the default support departments.</summary>
    private readonly IOptions<DefaultDepartmentsOptions> _departmentOptions;

    /// <summary>What visitors changed on the sample payment methods; forgotten on a reset.</summary>
    private readonly DemoCardState _cardState;

    /// <summary>What visitors did with the demo gateway's stored methods and charges; forgotten on a reset.</summary>
    private readonly DemoStoredCredentialState _credentialState;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DemoDataManager> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Creates the manager.</summary>
    /// <param name="db">The data context the seeders write through and the reset truncates.</param>
    /// <param name="users">Creates the demo accounts.</param>
    /// <param name="eventGate">Discards the domain events the seeded rows would raise.</param>
    /// <param name="options">The demo switch and the published accounts.</param>
    /// <param name="departmentOptions">The configured contact addresses of the default support departments.</param>
    /// <param name="cardState">What visitors changed on the sample payment methods; forgotten on a reset.</param>
    /// <param name="credentialState">What visitors did with the demo gateway's stored methods and charges; forgotten on a reset.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The source of the current moment.</param>
    public DemoDataManager(
        AppDbContext db,
        UserManager<AppUser> users,
        IDomainEventGate eventGate,
        IOptions<DemoOptions> options,
        IOptions<DefaultDepartmentsOptions> departmentOptions,
        DemoCardState cardState,
        DemoStoredCredentialState credentialState,
        ILogger<DemoDataManager> logger,
        IClock clock)
    {
        _db = db;
        _users = users;
        _eventGate = eventGate;
        _options = options;
        _departmentOptions = departmentOptions;
        _cardState = cardState;
        _credentialState = credentialState;
        _logger = logger;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        // Seeded rows are fixtures, not business events: no welcome mails, no payment receipts.
        using var suppression = _eventGate.Suppress();
        await SeedAllAsync(ct);
        _logger.LogInformation("Demo data seeded.");
    }

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            using var suppression = _eventGate.Suppress();

            // Drop whatever the previous run (or a visitor) left tracked; the rows are about to go.
            _db.ChangeTracker.Clear();
            await TruncateAsync(ct);
            await SeedAllAsync(ct);

            await transaction.CommitAsync(ct);
        });

        // The sample methods come back exactly as seeded; only after the commit, so a failed reset keeps them.
        _cardState.Clear();
        _credentialState.Clear();
    }

    /// <summary>Runs every seeder in dependency order.</summary>
    /// <param name="ct">Cancels the seed.</param>
    /// <returns>A task that completes once everything is saved.</returns>
    private async Task SeedAllAsync(CancellationToken ct)
    {
        // The two deployment-wide seeders every environment runs; both are idempotent and a reset
        // has just emptied the tables they fill.
        await DefaultDepartmentsSeeder.EnsureSeededAsync(_db, _departmentOptions.Value, ct);
        await PortalSettingsSeeder.EnsureSeededAsync(_db, ct);

        // One anchor for the whole run, so every date in every seeder is counted from the same day.
        var today = DemoAnchor.Today(_clock.UtcNow);

        await new DemoCatalogueSeeder(_db, today).SeedAsync(ct);
        var demoClientId = await new DemoPeopleSeeder(_db, _users, _options.Value, today).SeedAsync(ct);
        await new DemoServerSeeder(_db, today).SeedAsync(ct);
        await new DemoServiceSeeder(_db, today).SeedAsync(ct);
        await new DemoDomainSeeder(_db, today).SeedAsync(ct);
        await new DemoBillingSeeder(_db, today).SeedAsync(ct);
        await new DemoSalesSeeder(_db, today).SeedAsync(ct);
        await new DemoSupportSeeder(_db, today).SeedAsync(demoClientId, ct);
        await new DemoContentSeeder(_db, today).SeedAsync(ct);
        await new DemoLogSeeder(_db, today).SeedAsync(ct);

        // After the shared seeder created its empty rows at the top of this method.
        await new DemoPortalSettingsSeeder(_db, _options.Value).SeedAsync(ct);

        // The hook the storefront content (testimonials, partners, projects, FAQs) is filled through.
        await new DemoStorefrontSeeder(_db, today).SeedAsync(ct);
    }

    /// <summary>Empties every mapped table except the kept identity role tables and the tables the migrations seed through <c>HasData</c> (email templates among them), which nothing would restore.</summary>
    /// <param name="ct">Cancels the statement.</param>
    /// <returns>A task that completes once the tables are empty.</returns>
    private async Task TruncateAsync(CancellationToken ct)
    {
        // The design-time model, not _db.Model: the runtime model is read-optimized and refuses
        // GetSeedData, which is exactly how the migrations' HasData rows are told apart here.
        var tables = _db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null
                && !KeptTables.Contains(t.GetTableName()!)
                && !t.GetSeedData().Any())
            .Select(t => (Schema: t.GetSchema() ?? t.Model.GetDefaultSchema(), Table: t.GetTableName()!))
            .Distinct()
            .Select(t => t.Schema is null ? $"\"{t.Table}\"" : $"\"{t.Schema}\".\"{t.Table}\"")
            .ToList();

        // ExecuteSqlRaw is used because TRUNCATE has no parameter or LINQ form and the table list is
        // only known from the model at runtime. Every identifier is quoted and comes from the EF
        // model's table and schema names, never from a request, so nothing here is injectable.
        // CASCADE for the foreign keys between the listed tables; RESTART IDENTITY so ids repeat
        // after every reset and the demo's URLs stay stable.
#pragma warning disable EF1002 // The identifiers come from the EF model, never from a request.
        await _db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", ct);
#pragma warning restore EF1002
    }
}
