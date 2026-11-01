using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="AllowUnsignedSettingReader"/> reads the admin setting straight from the database while the container is
/// still being built: <c>true</c> in the row reads as on, anything else, a missing table (a first start before
/// migrations) and a database that cannot be reached read as off with a logged warning.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, whose database holds the seeded row.</param>
public sealed class AllowUnsignedSettingReaderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The setting reads as on exactly when its row holds <c>true</c>, and off again afterwards.</summary>
    [Fact]
    public async Task TheRow_DecidesTheAnswerAsync()
    {
        var connectionString = ConnectionString();
        var logger = new RecordingLoggerProvider().CreateLogger("t");

        AllowUnsignedSettingReader.Read(connectionString, logger).Should().BeFalse("the row is seeded off");

        await SetAsync(PluginSettingKeys.On);
        try
        {
            AllowUnsignedSettingReader.Read(connectionString, logger).Should().BeTrue();
        }
        finally
        {
            await SetAsync(PluginSettingKeys.Off);
        }

        AllowUnsignedSettingReader.Read(connectionString, logger).Should().BeFalse();
    }

    /// <summary>A database without the <c>Settings</c> table (a first start) reads as off, with a warning.</summary>
    [Fact]
    public void ADatabaseWithoutTheTable_ReadsAsOff()
    {
        var emptyDatabase = new NpgsqlConnectionStringBuilder(ConnectionString()) { Database = "postgres" }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(emptyDatabase, logs.CreateLogger("t")).Should().BeFalse();

        logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains(PluginSettingKeys.AllowUnsigned));
    }

    /// <summary>A database that cannot be reached reads as off, with a warning, instead of stopping the start.</summary>
    [Fact]
    public void AnUnreachableDatabase_ReadsAsOff()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(ConnectionString()) { Host = "127.0.0.1", Port = 1 }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(unreachable, logs.CreateLogger("t")).Should().BeFalse();

        logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// A connection string the driver rejects with a non Npgsql exception still reads as off, and the warning carries
    /// only the setting name and the exception type, never the connection string, its password or its host.
    /// </summary>
    [Fact]
    public void AnUnparsableConnectionString_ReadsAsOffWithoutLeakingIt()
    {
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read("Host=secret-host.internal;Password=hunter2;Port=not-a-number", logs.CreateLogger("t"))
            .Should().BeFalse();

        var entry = logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
        entry.Message.Should().Contain(PluginSettingKeys.AllowUnsigned).And.Contain("Exception");
        entry.Message.Should().NotContain("hunter2").And.NotContain("secret-host");
    }

    /// <summary>A failure while reaching an unreachable host does not log the host or the password either.</summary>
    [Fact]
    public void AnUnreachableDatabase_DoesNotLogTheHostOrPassword()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(ConnectionString()) { Host = "127.0.0.1", Port = 1, Password = "hunter2" }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(unreachable, logs.CreateLogger("t")).Should().BeFalse();

        var entry = logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
        entry.Message.Should().NotContain("127.0.0.1").And.NotContain("hunter2");
    }

    /// <summary>No connection string at all reads as off.</summary>
    /// <param name="connectionString">A missing or blank connection string.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoConnectionString_ReadsAsOff(string? connectionString)
    {
        AllowUnsignedSettingReader.Read(connectionString, new RecordingLoggerProvider().CreateLogger("t")).Should().BeFalse();
    }

    /// <summary>The factory database's connection string.</summary>
    /// <returns>The connection string.</returns>
    private string ConnectionString()
    {
        return factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
    }

    /// <summary>Writes the setting's row directly, as an earlier save would have.</summary>
    /// <param name="value">The value to store.</param>
    private async Task SetAsync(string value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Settings.SingleAsync(s => s.Key == PluginSettingKeys.AllowUnsigned);
        row.UpdateValue(value);
        await db.SaveChangesAsync();
    }
}
