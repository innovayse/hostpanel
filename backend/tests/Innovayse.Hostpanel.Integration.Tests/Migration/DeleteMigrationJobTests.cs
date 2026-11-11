using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>DELETE /api/admin/migrations/{id}</c>, dispatching <c>DeleteMigrationJobCommand</c>: deletes
/// a migration job the operator no longer wants to keep a record of.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteMigrationJobTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting an existing job removes it; getting it afterwards answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRemovesTheJob()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, "http://delete-me.example.test");

        var deleted = await client.DeleteAsync($"/api/admin/migrations/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterwards = await client.GetAsync($"/api/admin/migrations/{id}");
        afterwards.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(afterwards, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>Deleting a job removes its import report with it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRemovesTheJobsLogs()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, "http://delete-logs.example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.MigrationLogs.Add(MigrationLog.Create(
                id, MigrationEntityType.Clients, "someone@example.test", MigrationLogAction.Imported));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var deleted = await client.DeleteAsync($"/api/admin/migrations/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var reread = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await reread.MigrationLogs.AnyAsync(l => l.JobId == id, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>An id naming no job is refused as missing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesAnUnknownId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        const int missing = 987654321;

        var response = await client.DeleteAsync($"/api/admin/migrations/{missing}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/admin/migrations/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive job id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesANonPositiveId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/migrations/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await MigrationTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
