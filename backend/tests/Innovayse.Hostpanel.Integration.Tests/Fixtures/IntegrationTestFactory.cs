using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Registrar;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Creates a database of its own on the shared PostgreSQL server (<see cref="SharedPostgresServer"/>) and hosts the API
/// in-process using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Share across test classes via <see cref="IClassFixture{T}"/>.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Name of the database this factory owns on the shared server. Unique per factory, so test classes
    /// (and the tests that build a factory of their own) never see each other's rows.
    /// </summary>
    private readonly string _databaseName = $"it_{Guid.NewGuid():N}";

    /// <summary>
    /// Whether the host keeps every adapter the composition root registered instead of swapping the
    /// network-bound ones for test doubles. Set only by <see cref="WithProductionAdapters"/>.
    /// </summary>
    private readonly bool _keepsProductionAdapters;

    /// <summary>Connection string of <see cref="_databaseName"/>; set by <see cref="InitializeAsync"/>.</summary>
    private string? _connectionString;

    /// <summary>
    /// Lets the API's entry point start the host under the test server. <c>Program</c> ends in
    /// <c>RunJasperFxCommands</c>, which otherwise reads the arguments the test host passes as a
    /// JasperFx command line, prints its usage and never starts the host, so every request then fails
    /// with "the server has not been started".
    /// </summary>
    static IntegrationTestFactory()
    {
        JasperFxEnvironment.AutoStartHost = true;
    }

    /// <summary>
    /// Creates the factory. Its clients reach the host over HTTPS, as a browser reaches a deployed
    /// host, because outside Development the session cookie is always <c>Secure</c>: the client's
    /// cookie jar sends it back only over HTTPS. Hosts derived with <c>WithWebHostBuilder</c> copy
    /// these client options. A test about plain HTTP asks for an <c>http://</c> client itself.
    /// </summary>
    public IntegrationTestFactory()
        : this(keepsProductionAdapters: false)
    {
    }

    /// <summary>Creates the factory, with or without the test doubles.</summary>
    /// <param name="keepsProductionAdapters">Whether every adapter the composition root registered is kept.</param>
    private IntegrationTestFactory(bool keepsProductionAdapters)
    {
        _keepsProductionAdapters = keepsProductionAdapters;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    /// <summary>
    /// Creates a factory whose host keeps the production container: no adapter is replaced by a test
    /// double, and the registrations the composition root made are published as a
    /// <see cref="ServiceRegistrationSnapshot"/>. For the guards that must judge what a deployment runs,
    /// such as the service location rule, which a public test double would otherwise hide. Its hosts
    /// would reach real networks if a test sent them a message, so nothing is dispatched on them.
    /// The caller owns the factory and runs <see cref="InitializeAsync"/> before using it.
    /// </summary>
    /// <returns>A factory whose hosts carry only production registrations.</returns>
    public static IntegrationTestFactory WithProductionAdapters() => new(keepsProductionAdapters: true);

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Auth:Mode and EncryptionKey come from appsettings.Testing.json, which the Testing
        // environment loads before the first line of Program.cs runs; see the comment there.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Read when the DbContext is first resolved, long after Build(), so this one
            // is safe to add here, and it has to be, because the container it names is
            // not started until InitializeAsync runs.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
                    ?? throw new InvalidOperationException("InitializeAsync must run before the host is built.")
            });
        });

        // Suppress Windows EventLog provider to prevent ObjectDisposedException on teardown
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // This callback runs after the composition root, so the list is what a deployment with
            // this configuration registers; taken before the snapshot itself is added.
            if (_keepsProductionAdapters)
            {
                services.AddSingleton(new ServiceRegistrationSnapshot(services.ToList()));
                return;
            }

            // Replace the real registrar provider with a no-op stub so tests never hit the network.
            services.RemoveAll<IRegistrarProvider>();
            services.AddScoped<IRegistrarProvider, StubRegistrarProvider>();

            // A domain registered at the default records the default module, and the factory then
            // answers every later operation on it; that module must reach the same stub.
            services.RemoveAll<IRegistrarProviderFactory>();
            services.AddScoped<RegistrarProviderFactory>();
            services.AddScoped<IRegistrarProviderFactory, DefaultRegistrarProviderFactory>();

            // Replace real control panel provisioning provider with a no-op stub.
            services.RemoveAll<IProvisioningProvider>();
            services.AddScoped<IProvisioningProvider, StubProvisioningProvider>();

            // And the factory that actually gets asked for one. ProvisionServiceHandler
            // never resolves IProvisioningProvider; it calls
            // IProvisioningProviderFactory.CreateFor(server), and the real implementation
            // builds a hosting panel (v7) client aimed at the server row's hostname. Stubbing only the
            // interface above would leave every provisioning test making a real network call to
            // a host that does not exist, answered as 400 by the endpoint.
            services.RemoveAll<IProvisioningProviderFactory>();
            services.AddScoped<IProvisioningProviderFactory, StubProvisioningProviderFactory>();

            // SSH key agents are reached over SSH by the real factory; every test instead gets one
            // in-memory agent per server id, shared for the host's lifetime so a test can read back
            // what the handlers sent to the server it seeded.
            services.RemoveAll<ISshKeyProviderFactory>();
            services.AddSingleton<ISshKeyProviderFactory, FakeSshKeyProviderFactory>();
            services.RemoveAll<ISshHostKeyScanner>();
            services.AddSingleton<ISshHostKeyScanner, FakeSshHostKeyScanner>();

            // Registration sends the verification mail, so a host that kept the SMTP sender would
            // wait on a relay that does not exist for every account a test creates. Tests that read the
            // mail back derive a host with their own FakeEmailSender (EventHandlerTestHost.WithFakeRelay),
            // so nothing reads this one back and it keeps the production scoped lifetime.
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, FakeEmailSender>();

            // The hosting probe dials the domain's own nameservers and TLS port; tests answer from a
            // recording fake instead, so none of them reaches the network.
            services.RemoveAll<IHostingProbe>();
            services.AddSingleton<IHostingProbe, FakeHostingProbe>();

            // The GitHub releases client would reach api.github.com on every CheckReleaseUpdatesCron
            // run; all integration tests get a configurable in-memory fake instead.
            services.RemoveAll<IReleaseFeed>();
            services.AddSingleton<FakeReleaseFeed>();
            services.AddSingleton<IReleaseFeed>(sp => sp.GetRequiredService<FakeReleaseFeed>());

            // The installed version provider reads the entry assembly's informational version, which
            // carries no version attribute in the test runner; replace it with a fake that tests can
            // set to a deterministic baseline (default "1.0.0").
            services.RemoveAll<IInstalledVersionProvider>();
            services.AddSingleton<FakeInstalledVersionProvider>();
            services.AddSingleton<IInstalledVersionProvider>(sp => sp.GetRequiredService<FakeInstalledVersionProvider>());
        });
    }

    /// <summary>
    /// Creates this factory's database on the shared PostgreSQL server (starting the server if this is the
    /// first factory of the run), applies EF Core migrations, and seeds the roles and
    /// storefront settings Program.cs would otherwise seed on a real startup outside Testing.
    /// </summary>
    public async Task InitializeAsync()
    {
        _connectionString = await SharedPostgresServer.CreateDatabaseAsync(_databaseName);

        // Apply EF Core migrations to test database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Seed roles after migration
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Reseller, Roles.Client })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Storefront settings: Program.cs seeds these on every real startup except
        // Testing (the block this factory's role-seeding above also stands in for), so
        // without this call the settings table is permanently empty in every Integration
        // test and GetSettings/GetPublicSettings/UpdateSetting have nothing to read or
        // write against.
        await Innovayse.Hostpanel.Application.Admin.Seeding.PortalSettingsSeeder.EnsureSeededAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>
    /// Releases the API host first (through <see cref="WebApplicationFactory{TEntryPoint}"/>, which also
    /// disposes every host derived with <c>WithWebHostBuilder</c>: service provider, hosted services,
    /// message bus, Npgsql data sources and EF caches), then drops the pooled connections to this
    /// factory's database and finally drops the database itself. This is the <see cref="IAsyncDisposable"/>
    /// override, so <c>await using var factory = new IntegrationTestFactory()</c>, which the tests that
    /// build a factory of their own rely on, releases the database as well.
    /// </summary>
    /// <returns>A task that completes when the host, the pool and the database are released.</returns>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (_connectionString is not null)
            {
                // Npgsql pools are keyed by connection string, so clearing this factory's pool leaves the
                // pools of the classes running beside it alone (ClearAllPools would not).
                using var connection = new NpgsqlConnection(_connectionString);
                NpgsqlConnection.ClearPool(connection);
                await SharedPostgresServer.DropDatabaseAsync(_databaseName);
                _connectionString = null;
            }
        }

        // Starting the host made Wolverine compile its handler code with Roslyn, which keeps the metadata
        // of every loaded assembly in native memory behind small managed wrappers. The collector only
        // counts managed bytes, so without this pass that memory outlives the host by minutes and piles
        // up across classes (issue 373). One collection per released host, not per test, is enough.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes through the <see cref="IAsyncDisposable"/> path; xUnit calls this one.</summary>
    /// <returns>A task that completes when the host, the pools and the container are released.</returns>
    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    /// <summary>
    /// Creates a seeded admin user (if not yet created) and returns a valid JWT access token.
    /// The admin user is assigned the Admin role directly via ASP.NET Core Identity.
    /// </summary>
    /// <returns>JWT access token string for the admin user.</returns>
    public async Task<string> GetAdminTokenAsync()
    {
        const string AdminEmail = "admin-seed@innovayse.test";
        const string AdminPassword = "Admin@Seed123!";

        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var existing = await userManager.FindByEmailAsync(AdminEmail);
        if (existing is null)
        {
            var user = new AppUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                FirstName = "Seeded",
                LastName = "Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, AdminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create seeded admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, Roles.Admin);
        }

        return await GetTokenAsync(AdminEmail, AdminPassword);
    }

    /// <summary>
    /// Logs in an existing user and returns the JWT access token.
    /// </summary>
    /// <param name="email">User email address.</param>
    /// <param name="password">User password.</param>
    /// <returns>JWT access token string.</returns>
    public async Task<string> GetClientTokenAsync(string email, string password) =>
        await GetTokenAsync(email, password);

    /// <summary>
    /// Posts to /api/auth/login and extracts the accessToken from the response.
    /// </summary>
    /// <param name="email">User email.</param>
    /// <param name="password">User password.</param>
    /// <returns>JWT access token string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when login fails or token is missing.</exception>
    private async Task<string> GetTokenAsync(string email, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Login failed for {email}: {response.StatusCode} — {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("accessToken missing from login response.");
    }
}
