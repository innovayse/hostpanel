using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Every column in <see cref="ProtectedColumns"/> is stored encrypted: the raw value in the table
/// is not the plain input, a read through the model gives the plain input back, rows written
/// before encryption was required stay readable, and a context cannot be built without the
/// encryption service.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProtectedColumnsTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A plain credential used as the input of the tests.</summary>
    private const string Plain = "plain-credential-value-1";

    /// <summary>A service's password is stored encrypted and read back plain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientServicePassword_IsEncryptedAtRestAsync()
    {
        var id = await InsertServiceAsync(Plain);

        var raw = await RawAsync("ClientServices", "Password", id);

        AssertProtected(raw);
        (await ReloadServicePasswordAsync(id)).Should().Be(Plain);
    }

    /// <summary>A server's password, API token and access hash are stored encrypted and read back plain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServerCredentials_AreEncryptedAtRestAsync()
    {
        var id = await InsertServerAsync("server-password", "server-api-token", "server-access-hash");

        AssertProtected(await RawAsync("Servers", "Password", id));
        AssertProtected(await RawAsync("Servers", "ApiToken", id));
        AssertProtected(await RawAsync("Servers", "AccessHash", id));

        using var scope = factory.Services.CreateScope();
        var server = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Servers.AsNoTracking().SingleAsync(s => s.Id == id);
        server.Password.Should().Be("server-password");
        server.ApiToken.Should().Be("server-api-token");
        server.AccessHash.Should().Be("server-access-hash");
    }

    /// <summary>A server's SSH agent private key is stored encrypted and read back plain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AgentPrivateKey_IsEncryptedAtRestAsync()
    {
        var serverId = await InsertServerAsync(null, null, null);
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = ServerSshSettings.CreateFor(serverId);
            settings.Configure(true, "ssh.example.test", 22, "agent.example.test", 8022, "agentuser", "private-key-body", "SHA256:" + new string('A', 43), 10, 30);
            context.ServerSshSettings.Add(settings);
            await context.SaveChangesAsync();
            id = settings.Id;
        }

        AssertProtected(await RawAsync("ServerSshSettings", "AgentPrivateKey", id));

        using var readScope = factory.Services.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ServerSshSettings.AsNoTracking().SingleAsync(s => s.Id == id);
        stored.AgentPrivateKey.Should().Be("private-key-body");
    }

    /// <summary>A user's active and pending two-factor secrets are stored encrypted and read back plain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoFactorSecrets_AreEncryptedAtRestAsync()
    {
        var id = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(new AppUser
            {
                Id = id,
                UserName = $"{id}@example.test",
                FirstName = "Test",
                LastName = "User",
                TwoFactorSecret = "JBSWY3DPEHPK3PXP",
                PendingTwoFactorSecret = "KRSXG5CTMVRXEZLU",
            });
            await context.SaveChangesAsync();
        }

        AssertProtected(await RawAsync("AspNetUsers", "TwoFactorSecret", id));
        AssertProtected(await RawAsync("AspNetUsers", "PendingTwoFactorSecret", id));

        using var readScope = factory.Services.CreateScope();
        var user = await readScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.AsNoTracking().SingleAsync(u => u.Id == id);
        user.TwoFactorSecret.Should().Be("JBSWY3DPEHPK3PXP");
        user.PendingTwoFactorSecret.Should().Be("KRSXG5CTMVRXEZLU");
    }

    /// <summary>
    /// A row written before the marker existed, plain or encrypted by the earlier converter, is
    /// read as it is, and the next save of that column writes it encrypted and marked.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LegacyValues_StayReadable_AndAreWrittenProtectedOnNextSaveAsync()
    {
        var plainId = await InsertServiceAsync(Plain);
        var cipherId = await InsertServiceAsync(Plain);
        using (var scope = factory.Services.CreateScope())
        {
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"ClientServices\" SET \"Password\" = {0} WHERE \"Id\" = {1}", "legacy-plain-value", plainId);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"ClientServices\" SET \"Password\" = {0} WHERE \"Id\" = {1}", encryption.Encrypt("legacy-cipher-value"), cipherId);
        }

        (await ReloadServicePasswordAsync(plainId)).Should().Be("legacy-plain-value");
        (await ReloadServicePasswordAsync(cipherId)).Should().Be("legacy-cipher-value");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = await context.Set<ClientService>().SingleAsync(s => s.Id == plainId);
            service.AssignHostingAccount("legacy.example.test", "legacyuser", "rotated-value");
            await context.SaveChangesAsync();
        }

        AssertProtected(await RawAsync("ClientServices", "Password", plainId));
        (await ReloadServicePasswordAsync(plainId)).Should().Be("rotated-value");
    }

    /// <summary>A context cannot be built without the encryption service, so no model can store credentials in plain text.</summary>
    [Fact]
    public void Context_WithoutEncryptionService_ThrowsAtConstruction()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only").Options;

        var build = () => new AppDbContext(options, null!);

        build.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Every listed column carries the converter and the width sized for its encrypted form in the model.</summary>
    [Fact]
    public void Model_GivesEveryProtectedColumnTheConverterAndTheEncryptedWidth()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only").Options;
        using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
        var model = context.GetService<IDesignTimeModel>().Model;

        foreach (var column in ProtectedColumns.All)
        {
            var property = model.FindEntityType(column.Entity)!.FindProperty(column.Property)!;
            property.GetValueConverter().Should().BeOfType<EncryptedStringConverter>($"{column.Entity.Name}.{column.Property} is protected");
            property.GetMaxLength().Should().Be(ProtectedColumnWidth.For(column.MaxPlainLength));
        }
    }

    /// <summary>The encrypted form of the longest allowed value, in the widest characters, still fits its column.</summary>
    /// <param name="maxPlainChars">The longest plain value, in characters.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(256)]
    [InlineData(2000)]
    [InlineData(8000)]
    public void Width_HoldsTheEncryptedFormOfTheLongestValue(int maxPlainChars)
    {
        var encryption = new AesEncryptionService(Convert.ToBase64String(new byte[32]));
        var longest = string.Concat(Enumerable.Repeat("\U0001F512", maxPlainChars));

        var stored = SecretSettingCipher.Marker + encryption.Encrypt(longest);

        stored.Length.Should().BeLessThanOrEqualTo(ProtectedColumnWidth.For(maxPlainChars));
    }

    /// <summary>Asserts a raw column value is the marked ciphertext and not any of the plain inputs.</summary>
    /// <param name="raw">The value as the table holds it.</param>
    private static void AssertProtected(string? raw)
    {
        raw.Should().NotBeNullOrEmpty();
        raw.Should().StartWith(SecretSettingCipher.Marker);
        raw.Should().NotContain("plain-credential").And.NotContain("server-").And.NotContain("private-key").And.NotContain("JBSWY").And.NotContain("KRSXG");
    }

    /// <summary>Reads one column of one row with raw SQL, bypassing the model's converter.</summary>
    /// <param name="table">The table name.</param>
    /// <param name="column">The column name.</param>
    /// <param name="id">The row key.</param>
    /// <returns>The stored value.</returns>
    private async Task<string?> RawAsync(string table, string column, object id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Table and column names cannot be parameters; they come from this test's own literals, the key stays a parameter.
        var sql = $"SELECT \"{column}\" AS \"Value\" FROM \"{table}\" WHERE \"Id\" = {{0}}";
        var rows = await context.Database.SqlQueryRaw<string>(sql, id).ToListAsync();
        return rows.Single();
    }

    /// <summary>Inserts a service with a hosting account password.</summary>
    /// <param name="password">The plain password.</param>
    /// <returns>The new service's id.</returns>
    private async Task<int> InsertServiceAsync(string password)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var service = ClientService.Create(clientId, productId, billingCycle: BillingCycle.Monthly, now: DateTimeOffset.UtcNow);
        service.AssignHostingAccount("example.test", "someuser", password);
        context.Set<ClientService>().Add(service);
        await context.SaveChangesAsync();
        return service.Id;
    }

    /// <summary>Reads a service's password back through the model.</summary>
    /// <param name="id">The service id.</param>
    /// <returns>The plain password.</returns>
    private async Task<string?> ReloadServicePasswordAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<ClientService>().AsNoTracking().Where(s => s.Id == id).Select(s => s.Password).SingleAsync();
    }

    /// <summary>Inserts a server with the given credentials.</summary>
    /// <param name="password">The plain password, or null.</param>
    /// <param name="apiToken">The plain API token, or null.</param>
    /// <param name="accessHash">The plain access hash, or null.</param>
    /// <returns>The new server's id.</returns>
    private async Task<int> InsertServerAsync(string? password, string? apiToken, string? accessHash)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var server = Server.Create(
            new ServerDetails(
                $"protected-{Guid.NewGuid():N}", "server.example.test", null, null, ServerModule.CPanel, "root",
                new ServerCredentials(password, apiToken, accessHash), true, null, false, false, 0m, null, null,
                ServerNameServers.None),
            DateTimeOffset.UtcNow);
        context.Servers.Add(server);
        await context.SaveChangesAsync();
        return server.Id;
    }
}
