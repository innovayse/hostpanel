using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>POST /api/admin/migrations/{id}/retry</c>, dispatching <c>RetryMigrationJobCommand</c>:
/// queues a failed job's pull again.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RetryMigrationJobTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A failed job is queued again and its failure cleared.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RetryOnAFailedJobQueuesItAsync()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, MigrationTestSeeds.UnreachableSourceUrl);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var job = await context.MigrationJobs.FirstAsync(j => j.Id == id);
            job.Queue(TestMoments.Seeded);
            job.Fail(MigrationJobFailure.Interrupted, TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await client.PostAsync($"/api/admin/migrations/{id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var check = factory.Services.CreateScope();
        var reloaded = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == id);
        reloaded.Failure.Should().NotBe(MigrationJobFailure.Interrupted);
    }

    /// <summary>A job that has not failed is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RetryRefusesAJobThatHasNotFailedAsync()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, MigrationTestSeeds.UnreachableSourceUrl);

        var response = await client.PostAsync($"/api/admin/migrations/{id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FAILED);
    }

    /// <summary>An id naming no job is refused as missing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RetryRefusesAnUnknownIdAsync()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/admin/migrations/987654321/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RetryRefusesAnonymousAsync()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/migrations/1/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
