using Innovayse.Hostpanel.API.Extensions;
using JasperFx;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine.Runtime.Handlers;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// How a host that carries only <see cref="MessagingRegistration.AddHostpanelWolverine"/> starts: the
/// startup assertion on the generated handler code, the refusal to start when that code is missing, and the
/// upgrade of a message store schema that an earlier Wolverine created. None of them needs the whole API,
/// which is why each builds the smallest host that has the registration under test.
/// </summary>
public sealed class WolverineStartupTests
{
    /// <summary>The file, copied beside the test assembly, that holds the schema Wolverine 5.41 created.</summary>
    private const string FiveFortyOneSchemaFile = "wolverine-5.41-schema.sql";

    /// <summary>
    /// The environment check over the generated code is on where handler code is loaded (every
    /// environment but Development and Testing) and off where it is compiled on demand, since a
    /// development or test host starts with no generated code at all.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="expected">Whether the active JasperFx profile asserts the pre-generated types.</param>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Demo", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void PreGeneratedTypeAssertion_IsOnWhereHandlerCodeIsLoaded(string environment, bool expected)
    {
        // The Development row only covers the AssertsPreGeneratedTypes branch: a Development host may
        // compile on demand, so it is never given the profile that asserts the generated types.
        using var app = BuildHost(environment, "Host=unused;Database=unused");

        var options = app.Services.GetRequiredService<JasperFxOptions>();

        options.ActiveProfile.AssertAllPreGeneratedTypesExist.Should().Be(expected);
        MessagingRegistration.AssertsPreGeneratedTypes(MessagingRegistration.TypeLoadModeFor(app.Environment))
            .Should().Be(expected);
    }

    /// <summary>
    /// A production host whose assembly holds no generated handler code (a skipped or stale
    /// <c>codegen write</c>) must not start: before Wolverine 6.30 it started healthy and lost every message
    /// of the missing type. The exception names the chains it could not load.
    /// </summary>
    /// <returns>A task that completes when the refusal was observed.</returns>
    [Fact]
    public async Task StaticHost_WithoutGeneratedHandlerCode_RefusesToStart()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var app = BuildHost("Production", database.ConnectionString);

        var start = () => app.StartAsync();

        var failure = await start.Should().ThrowAsync<MissingPreBuiltTypesException>();
        failure.Which.Message.Should().Contain("pre-built handler type");
    }

    /// <summary>
    /// The current Wolverine starts on a message store schema created by 5.41, the version production ran
    /// before: the additive upgrade at startup drops no table and no column, keeps the rows already stored,
    /// and leaves a scheduled message that was waiting exactly as it was. The upgrade did run: the dead
    /// letter history table and the replay count column of the dead letters, which 5.41 does not have, exist
    /// afterwards.
    /// </summary>
    /// <returns>A task that completes when the upgraded schema was compared with the original.</returns>
    [Fact]
    public async Task Host_StartedOnAFiveFortyOneSchema_UpgradesItAdditivelyAndKeepsItsRows()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", FiveFortyOneSchemaFile)));
        var envelopeId = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            INSERT INTO wolverine.wolverine_incoming_envelopes
                (id, status, owner_id, execution_time, attempts, body, message_type, received_at, keep_until)
            VALUES
                ('{0}', 'Scheduled', 0, now() + interval '30 days', 0, '\x00', 'upgrade.sentinel', 'local://upgrade/', NULL)
            """.Replace("{0}", envelopeId.ToString()));
        var before = await database.ColumnsOfWolverineSchemaAsync();
        before.Should().NotBeEmpty("the fixture is the schema 5.41 created");

        await using (var app = BuildHost("Testing", database.ConnectionString))
        {
            await app.StartAsync();
            await app.StopAsync();
        }

        var after = await database.ColumnsOfWolverineSchemaAsync();
        after.Should().Contain(before, "the upgrade is additive: no table and no column of the old schema is dropped");
        after.Should().Contain(
            ["wolverine_dead_letter_history.id", "wolverine_dead_letters.replay_count"],
            "the upgrade really ran and added what the current Wolverine keeps for dead letters");
        before.Should().NotContain(
            ["wolverine_dead_letter_history.id", "wolverine_dead_letters.replay_count"],
            "5.41 had neither, so their presence afterwards is the upgrade's doing");
        (await database.ScalarAsync($"SELECT status FROM wolverine.wolverine_incoming_envelopes WHERE id = '{envelopeId}'"))
            .Should().Be("Scheduled");
    }

    /// <summary>
    /// Builds the smallest web host that has Wolverine registered the way the API registers it: no
    /// controller, no database context, no bus listener, only the messaging registration and its message
    /// store. It listens on an ephemeral loopback port, so any number of them can run at once.
    /// </summary>
    /// <param name="environment">The hosting environment, which decides the code generation mode.</param>
    /// <param name="connectionString">The database Wolverine keeps its message store in.</param>
    /// <returns>The built, not yet started, host.</returns>
    private static WebApplication BuildHost(string environment, string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{MessagingRegistration.ConnectionStringName}"] = connectionString,
        });
        builder.Logging.ClearProviders();

        // Development validates every registration when the container is built, and this host has only the
        // messaging registration, so the middleware that wants the caller's identity could not be built.
        // Nothing here dispatches a message; the container is only asked for the options.
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddHostpanelWolverine();

        return builder.Build();
    }
}
