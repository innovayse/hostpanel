namespace Innovayse.Integration.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Integration.Tests.Domains;
using Innovayse.Integration.Tests.Provisioning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

/// <summary>
/// Spins up a real PostgreSQL container via Testcontainers and hosts the API
/// in-process using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Share across test classes via <see cref="IClassFixture{T}"/>.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// AES-256 key for the test host: 32 bytes, Base64. Fixed rather than generated so a
    /// failing run can be reproduced, and harmless in the open because the database it
    /// protects is a throwaway container that lives for the length of one test class.
    /// </summary>
    private const string TestEncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    /// <summary>PostgreSQL container started before each test class.</summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("innovayse_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration, for these two. Program.cs is
        // top-level code: it reads Auth:Mode and passes the configuration to
        // AddInfrastructure — which reads EncryptionKey — while composing the builder,
        // and ConfigureAppConfiguration's sources are not added until builder.Build().
        // Set the late way, both keys were simply absent at the moment they were read:
        // AddInnovayseAuth ran (mode was not "local") and threw "Auth:AppName is
        // required", the catch in Program.cs logged it, and all 41 tests reported "The
        // entry point exited without ever building an IHost". UseSetting writes into the
        // host configuration, which exists before the first line of Program.cs runs.
        //
        // Auth:Mode: every test here creates its users through POST /api/auth/register —
        // the only user-creation path in the suite — and LocalAuthController 404s that
        // action unless the mode is local (sso by default, per docker-compose.yml's
        // AUTH_MODE:-sso).
        //
        // EncryptionKey: AddInfrastructure refuses to start outside Development without
        // one, because an absent key silently wrote server credentials and client-service
        // passwords to the database in plain text. A real key rather than an exemption,
        // so these tests exercise the encrypting EF converters that production uses.
        builder.UseSetting("Auth:Mode", "local");
        builder.UseSetting("EncryptionKey", TestEncryptionKey);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Read when the DbContext is first resolved, long after Build(), so this one
            // is safe to add here — and it has to be, because the container it names is
            // not started until InitializeAsync runs.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
            });
        });

        // Suppress Windows EventLog provider to prevent ObjectDisposedException on teardown
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Replace real Namecheap registrar with a no-op stub so tests never hit the network.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRegistrarProvider));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IRegistrarProvider, StubRegistrarProvider>();

            // Replace real cPanel provisioning provider with a no-op stub.
            var provisioningDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProvisioningProvider));
            if (provisioningDescriptor is not null)
            {
                services.Remove(provisioningDescriptor);
            }

            services.AddScoped<IProvisioningProvider, StubProvisioningProvider>();

            // And the factory that actually gets asked for one. ProvisionServiceHandler
            // never resolves IProvisioningProvider — it calls
            // IProvisioningProviderFactory.CreateFor(server), and the real implementation
            // builds a CWP7 client aimed at the server row's hostname. Stubbing only the
            // interface above left every provisioning test making a real network call to
            // a host that does not exist, which came back as 400 from the endpoint.
            var providerFactoryDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProvisioningProviderFactory));
            if (providerFactoryDescriptor is not null)
            {
                services.Remove(providerFactoryDescriptor);
            }

            services.AddScoped<IProvisioningProviderFactory, StubProvisioningProviderFactory>();
        });
    }

    /// <summary>Starts the PostgreSQL container and applies EF Core migrations.</summary>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

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
    }

    /// <summary>Stops and disposes the PostgreSQL container after tests complete.</summary>
    public new async Task DisposeAsync() => await _postgres.DisposeAsync();

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
