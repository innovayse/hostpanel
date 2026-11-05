using System.Data.Common;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host with four enabled app plugins, built once for a test class, with every log line recorded: three whose manifests
/// declare <c>hostAccess: ["ssh"]</c> (one listing every product in its own manifest) and one that does not. Each puts a service tab widget into
/// <c>client.service.tabs</c>, the widget an operator binds to products (#823), which is what lets a plugin reach a
/// service's hosting. Tests reach the SSH methods the way a plugin does: through the <see cref="IPluginHostApi"/> that
/// <see cref="IPluginCallServices"/> builds for a binding.
/// </summary>
public sealed class PluginSshFixture : IAsyncLifetime
{
    /// <summary>Every log line the host wrote, for asserting the private key never reached a log.</summary>
    private readonly RecordingLoggerProvider _logs = new();

    /// <summary>Gets the shared API and database the host is derived from.</summary>
    public IntegrationTestFactory Factory { get; } = new();

    /// <summary>Gets the host whose registry holds the two plugins.</summary>
    public WebApplicationFactory<Program> Host { get; private set; } = null!;

    /// <summary>Gets the id of the enabled plugin that declares SSH host access.</summary>
    public string SshPluginId { get; } = PluginActivationTestSeeds.NewId("ssh");

    /// <summary>Gets the id of a second enabled plugin that declares SSH host access, which must never reach the first one's keys.</summary>
    public string OtherSshPluginId { get; } = PluginActivationTestSeeds.NewId("ssh-other");

    /// <summary>The first line of every OpenSSH private key file.</summary>
    public const string PrivateKeyHeader = "-----BEGIN OPENSSH PRIVATE KEY-----";

    /// <summary>
    /// Gets the id of an enabled plugin that declares SSH host access and whose service tab widget's manifest lists every
    /// product id up to <see cref="GreedyProductIdCeiling"/>, standing for a plugin author's claim; no operator binds it.
    /// </summary>
    public string GreedySshPluginId { get; } = PluginActivationTestSeeds.NewId("ssh-greedy");

    /// <summary>The highest product id the greedy plugin's manifest lists, far above any product a test seeds.</summary>
    public const int GreedyProductIdCeiling = 100_000;

    /// <summary>Gets the id of an enabled plugin that declares no host access.</summary>
    public string PlainPluginId { get; } = PluginActivationTestSeeds.NewId("plain");

    /// <summary>The id of the service tab widget every test plugin declares; its products are the plugin's bound products.</summary>
    public const string ServiceTabWidget = "ssh-tab";

    /// <summary>Gets every message the host logged so far.</summary>
    public IReadOnlyList<string> LoggedMessages => _logs.Entries.Select(e => e.Message).ToList();

    /// <summary>Builds the database and the derived host with both plugins registered and switched on.</summary>
    /// <returns>A task that completes when the host is ready.</returns>
    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        var registry = PluginActivationTestSeeds.Registry(
            Plugin(SshPluginId, [PluginHostAccess.Ssh]),
            Plugin(OtherSshPluginId, [PluginHostAccess.Ssh]),
            Plugin(GreedySshPluginId, [PluginHostAccess.Ssh], [.. Enumerable.Range(1, GreedyProductIdCeiling)]),
            Plugin(PlainPluginId, []));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PluginActivationTestSeeds.EnableAsync(db, SshPluginId, OtherSshPluginId, PlainPluginId, GreedySshPluginId);
        }

        Host = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(_logs);
                logging.SetMinimumLevel(LogLevel.Trace);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PluginRegistry>();
                services.RemoveAll<IPluginRegistry>();
                services.AddSingleton(registry);

                // The operator's binding route reads the loaded manifests through the port, so it must see these plugins too.
                services.AddSingleton<IPluginRegistry>(registry);
            });
        });
    }

    /// <summary>Releases the host and the factory.</summary>
    /// <returns>A task that completes when both are released.</returns>
    public async Task DisposeAsync()
    {
        Host.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>The fake key agents of <see cref="Host"/>, which are the ones its calls reach.</summary>
    /// <returns>The agents.</returns>
    public FakeSshKeyProviderFactory Agents()
    {
        return (FakeSshKeyProviderFactory)Host.Services.GetRequiredService<ISshKeyProviderFactory>();
    }

    /// <summary>Makes a client of <see cref="Host"/> carrying an administrator's bearer token.</summary>
    /// <returns>The client, to dispose when the test ends.</returns>
    public async Task<HttpClient> AdminClientAsync()
    {
        var http = Host.CreateClient();
        var token = await Factory.GetAdminTokenAsync();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>Reads the product of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>Its product id.</returns>
    public async Task<int> ProductOfAsync(int serviceId)
    {
        using var scope = Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ClientServices.Where(s => s.Id == serviceId).Select(s => s.ProductId).SingleAsync();
    }

    /// <summary>Seeds a client with one active service on a fresh SSH-enabled server.</summary>
    /// <param name="label">Label for the generated e-mail.</param>
    /// <returns>The owner, server and service.</returns>
    public Task<SshOwnerScenario> OwnerWithServiceAsync(string label)
    {
        return SshTestSeeds.OwnerWithServiceAsync(Factory, label);
    }

    /// <summary>
    /// Binds exactly the products of <paramref name="serviceIds"/> to <paramref name="pluginId"/>'s service tab widget
    /// through the admin route an operator uses (#823), replacing any earlier binding; no services unbinds every product.
    /// </summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="serviceIds">Services whose products are bound.</param>
    /// <returns>A task that completes when the binding is stored.</returns>
    public async Task BindProductsOfAsync(string pluginId, params int[] serviceIds)
    {
        var productIds = new List<int>();
        foreach (var serviceId in serviceIds)
        {
            productIds.Add(await ProductOfAsync(serviceId));
        }

        using var admin = await AdminClientAsync();
        var response = await admin.PutAsJsonAsync(
            $"/api/admin/plugins/{pluginId}/slot-widgets/{PluginSlots.ClientServiceTabs}/{ServiceTabWidget}", new { productIds });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Switches a plugin on or off the way the admin Integrations page does, through its switch row.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="enabled">Whether it is switched on.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    public async Task SwitchAsync(string pluginId, bool enabled)
    {
        using var scope = Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Settings.SingleAsync(s => s.Key == Domain.Settings.IntegrationSettingKeys.EnabledKey(pluginId));
        row.UpdateValue(enabled ? Domain.Settings.IntegrationSettingKeys.EnabledValue : Domain.Settings.IntegrationSettingKeys.DisabledValue);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Runs <paramref name="act"/> against the host API one plugin call is handed.</summary>
    /// <typeparam name="T">What the action answers.</typeparam>
    /// <param name="binding">The call.</param>
    /// <param name="act">What the plugin does with the host API.</param>
    /// <returns>What <paramref name="act"/> answered.</returns>
    public Task<T> AsPluginAsync<T>(PluginCallBinding binding, Func<IPluginHostApi, Task<T>> act)
    {
        return AsPluginAsync(Host, binding, act);
    }

    /// <summary>Runs <paramref name="act"/> against the host API one plugin call of <paramref name="host"/> is handed.</summary>
    /// <typeparam name="T">What the action answers.</typeparam>
    /// <param name="host">The host to call through, <see cref="Host"/> or one derived from it.</param>
    /// <param name="binding">The call.</param>
    /// <param name="act">What the plugin does with the host API.</param>
    /// <returns>What <paramref name="act"/> answered.</returns>
    public static async Task<T> AsPluginAsync<T>(
        WebApplicationFactory<Program> host, PluginCallBinding binding, Func<IPluginHostApi, Task<T>> act)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(binding);
        return await act(services.Host);
    }

    /// <summary>A client-area binding of a plugin to a client holding every permission.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <returns>The binding.</returns>
    public static PluginCallBinding ClientCall(string pluginId, int clientId)
    {
        return PluginCallBinding.ForClient(pluginId, clientId, $"ref-{clientId}", ClientPermission.All);
    }

    /// <summary>A client-area binding of a plugin to a client, with the caller holding only <paramref name="permissions"/>.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="permissions">What the caller (owner or contact) holds on the account.</param>
    /// <returns>The binding.</returns>
    public static PluginCallBinding ClientCall(string pluginId, int clientId, ClientPermission permissions)
    {
        return PluginCallBinding.ForClient(pluginId, clientId, $"ref-{clientId}", permissions);
    }

    /// <summary>Seeds a client with one active service on a fresh server whose SSH key management is switched off.</summary>
    /// <param name="label">Label for the generated e-mail and the server's host name.</param>
    /// <returns>The owner, server and service.</returns>
    public async Task<SshOwnerScenario> OwnerOnSshDisabledServerAsync(string label)
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(Factory, label);
        var serverId = await SshTestSeeds.SeedServerAsync(Factory, $"{label}.example.test", 22, enabled: false);
        var serviceId = await SshTestSeeds.SeedServiceAsync(Factory, registered.ClientId, serverId, "web");
        return new SshOwnerScenario(registered.Client, registered.ClientId, serverId, serviceId);
    }

    /// <summary>Loads the stored record of a key.</summary>
    /// <param name="keyId">The key.</param>
    /// <returns>The record, or <see langword="null"/> when none is stored.</returns>
    public async Task<SshKey?> KeyAsync(Guid keyId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.SshKeys.AsNoTracking().SingleOrDefaultAsync(k => k.KeyId == keyId);
    }

    /// <summary>Loads every stored key record of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The records.</returns>
    public async Task<IReadOnlyList<SshKey>> KeysOfAsync(int serviceId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.SshKeys.AsNoTracking().Where(k => k.ClientServiceId == serviceId).ToListAsync();
    }

    /// <summary>The activity log lines written for a client, oldest first.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The descriptions.</returns>
    public async Task<IReadOnlyList<string>> ActivityOfAsync(int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ActivityLogs.AsNoTracking()
            .Where(l => l.ClientId == clientId)
            .OrderBy(l => l.Id)
            .Select(l => l.Description)
            .ToListAsync();
    }

    /// <summary>
    /// Whether any text, JSON or byte column of any table, Wolverine's message store included, holds
    /// <paramref name="needle"/>. "Never stored" is a property of the whole database, which no query on one table proves.
    /// </summary>
    /// <param name="needle">The text to look for; base64 or PEM text, so it holds no <c>LIKE</c> wildcard.</param>
    /// <returns><see langword="true"/> when at least one row of one column holds it.</returns>
    public async Task<bool> DatabaseHoldsAsync(string needle)
    {
        using var scope = Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            foreach (var column in await ReadableColumnsAsync(connection))
            {
                if (await ColumnHoldsAsync(connection, column, needle))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>The base64 text of the private part of an OpenSSH private key: its last full body line.</summary>
    /// <param name="privateKey">The OpenSSH private key text.</param>
    /// <returns>A line that encodes only private key material, never the public blob the host keeps.</returns>
    public static string PrivateMaterialOf(string privateKey)
    {
        // The public key sits near the start of the file and is stored on purpose; the last full line encodes the
        // private scalar, so finding it anywhere proves a leak and finding the public key does not.
        var body = privateKey.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("-----", StringComparison.Ordinal))
            .ToList();
        return body[^2];
    }

    /// <summary>Lists every column of the application and message schemas whose values can hold text.</summary>
    /// <param name="connection">An open connection.</param>
    /// <returns>The columns.</returns>
    private static async Task<IReadOnlyList<DatabaseTextColumn>> ReadableColumnsAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_schema, table_name, column_name, data_type
            FROM information_schema.columns
            WHERE table_schema IN ('public', 'wolverine')
              AND data_type IN ('text', 'character varying', 'jsonb', 'json', 'bytea')
            """;
        var columns = new List<DatabaseTextColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new DatabaseTextColumn(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3) == "bytea"));
        }

        return columns;
    }

    /// <summary>Whether one column holds <paramref name="needle"/> in any row.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="column">The column.</param>
    /// <param name="needle">The text to look for.</param>
    /// <returns><see langword="true"/> when a row holds it.</returns>
    private static async Task<bool> ColumnHoldsAsync(DbConnection connection, DatabaseTextColumn column, string needle)
    {
        await using var command = connection.CreateCommand();
        var table = $"{Quote(column.Schema)}.{Quote(column.Table)}";
        var value = Quote(column.Column);
        command.CommandText = column.IsBinary
            ? $"SELECT EXISTS (SELECT 1 FROM {table} WHERE position(convert_to(@needle, 'UTF8') in {value}) > 0)"
            : $"SELECT EXISTS (SELECT 1 FROM {table} WHERE {value}::text LIKE '%' || @needle || '%')";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "needle";
        parameter.Value = needle;
        command.Parameters.Add(parameter);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Quotes an identifier read from the catalogue, so a PascalCase name keeps its case.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The double-quoted identifier.</returns>
    private static string Quote(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>Builds an app plugin whose manifest declares <paramref name="hostAccess"/>.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="hostAccess">The host access kinds the manifest declares.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, IReadOnlyList<string> hostAccess)
    {
        return Plugin(id, hostAccess, []);
    }

    /// <summary>Builds an app plugin whose manifest declares <paramref name="hostAccess"/> and lists products on its widget.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="hostAccess">The host access kinds the manifest declares.</param>
    /// <param name="productIds">The products the manifest's service tab widget lists.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, IReadOnlyList<string> hostAccess, IReadOnlyList<int> productIds)
    {
        return new LoadedPlugin(Manifest(id, hostAccess, productIds), typeof(FakeAppPlugin), PluginViewDocuments.None);
    }

    /// <summary>The manifest of a test app plugin.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="hostAccess">The host access kinds it declares.</param>
    /// <param name="productIds">The products its service tab widget lists.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string id, IReadOnlyList<string> hostAccess, IReadOnlyList<int> productIds)
    {
        return new PluginManifest
        {
            Id = id,
            Name = "SSH test plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Plugin used by the plugin SSH access tests.",
            Type = PluginType.App,
            Category = nameof(PluginType.App),
            EntryPoint = typeof(FakeAppPlugin).FullName ?? nameof(FakeAppPlugin),
            SdkVersion = "1.0",
            HostAccess = hostAccess,
            Ui = new PluginUi
            {
                Slots =
                [
                    new PluginSlotEntry
                    {
                        Id = ServiceTabWidget, Slot = PluginSlots.ClientServiceTabs, View = "views/tab.json",
                        ContextKeys = [PluginSlotContextKeys.ServiceId],
                        ProductIds = productIds,
                    },
                ],
            },
        };
    }
}
