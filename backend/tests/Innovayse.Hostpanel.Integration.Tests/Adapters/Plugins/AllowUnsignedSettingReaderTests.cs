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
/// migrations) and a database that cannot be reached read as off with a log entry: Information for the missing table, Error for the rest.
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

    /// <summary>A database without the <c>Settings</c> table (a first start) reads as off, logged at Information with its SQL state.</summary>
    [Fact]
    public void ADatabaseWithoutTheTable_ReadsAsOff()
    {
        var emptyDatabase = new NpgsqlConnectionStringBuilder(ConnectionString()) { Database = "postgres" }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(emptyDatabase, logs.CreateLogger("t")).Should().BeFalse();

        logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information
            && e.Message.Contains(PluginSettingKeys.AllowUnsigned)
            && e.Message.Contains("42P01"));
    }

    /// <summary>
    /// A database error other than the missing table (here a database that does not exist, SQL state 3D000) reads as
    /// off and is logged at Error with its SQL state, and without the database name.
    /// </summary>
    [Fact]
    public void AnotherDatabaseError_IsLoggedAtErrorWithItsSqlState()
    {
        var missingDatabase = new NpgsqlConnectionStringBuilder(ConnectionString())
        {
            Database = "no_such_database_for_the_reader_test",
        }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(missingDatabase, logs.CreateLogger("t")).Should().BeFalse();

        var entry = logs.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("3D000").And.NotContain("no_such_database_for_the_reader_test");
    }

    /// <summary>A database that cannot be reached reads as off, logged at Error, instead of stopping the start.</summary>
    [Fact]
    public void AnUnreachableDatabase_ReadsAsOff()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(ConnectionString()) { Host = "127.0.0.1", Port = 1 }.ConnectionString;
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read(unreachable, logs.CreateLogger("t")).Should().BeFalse();

        logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    /// <summary>
    /// A connection string the driver rejects with a non Npgsql exception still reads as off, and the error entry carries
    /// only the setting name and the exception type, never the connection string, its password or its host.
    /// </summary>
    [Fact]
    public void AnUnparsableConnectionString_ReadsAsOffWithoutLeakingIt()
    {
        var logs = new RecordingLoggerProvider();

        AllowUnsignedSettingReader.Read("Host=secret-host.internal;Password=hunter2;Port=not-a-number", logs.CreateLogger("t"))
            .Should().BeFalse();

        var entry = logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error).Subject;
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

        var entry = logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error).Subject;
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
