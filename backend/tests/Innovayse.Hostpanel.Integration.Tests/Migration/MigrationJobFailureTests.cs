using System.Net;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// How a failed pull is recorded: <see cref="MigrationPullWorker"/> stores a closed
/// <see cref="MigrationJobFailure"/> instead of the exception text, and rows written before that
/// still read back.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MigrationJobFailureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The text of the exception the fake source throws; it must never be stored.</summary>
    private const string SecretDetail = "connection to 10.0.0.5 refused, key abc123";

    /// <summary>Each kind of source failure is stored as its reason, and the exception text is not.</summary>
    /// <param name="status">The status the source answered with; null when it could not be reached.</param>
    /// <param name="expected">The reason the job must record.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, MigrationJobFailure.SourceRefused)]
    [InlineData(null, MigrationJobFailure.SourceUnreachable)]
    public async Task RunAsync_SourceFails_RecordsTheReasonOnlyAsync(HttpStatusCode? status, MigrationJobFailure expected)
    {
        var source = new FailingMigrationSource(new HttpRequestException(SecretDetail, inner: null, status));
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);

        await scope.ServiceProvider.GetRequiredService<MigrationPullWorker>().RunAsync(job.Id, CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Failed, reloaded.Status);
        Assert.Equal(expected, reloaded.Failure);
        var stored = await StoredErrorAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), job.Id);
        Assert.Equal(expected.ToString(), stored);
    }

    /// <summary>A source address this server may not call is recorded as its own reason, not as an unreachable source.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RunAsync_SourceAddressRefused_RecordsAddressNotAllowedAsync()
    {
        var source = new FailingMigrationSource(new MigrationSourceAddressRefusedException());
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);

        await scope.ServiceProvider.GetRequiredService<MigrationPullWorker>().RunAsync(job.Id, CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Failed, reloaded.Status);
        Assert.Equal(MigrationJobFailure.SourceAddressNotAllowed, reloaded.Failure);
        var stored = await StoredErrorAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), job.Id);
        Assert.Equal(nameof(MigrationJobFailure.SourceAddressNotAllowed), stored);
    }

    /// <summary>A pull stopped through its token is recorded as cancelled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Cancelled_RecordsCancelledAsync()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var source = new FailingMigrationSource(new OperationCanceledException(cancellation.Token));
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);

        await scope.ServiceProvider.GetRequiredService<MigrationPullWorker>().RunAsync(job.Id, cancellation.Token);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobFailure.Cancelled, reloaded.Failure);
    }

    /// <summary>A row written before the reason was a closed value, holding free text, reads back as unknown.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OldFreeTextRow_ReadsBackAsUnknownAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "MigrationJobs" SET "Status" = {nameof(MigrationJobStatus.Failed)}, "ErrorMessage" = {SecretDetail} WHERE "Id" = {job.Id}""",
            CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Failed, reloaded.Status);
        Assert.Equal(MigrationJobFailure.Unknown, reloaded.Failure);
    }

    /// <summary>Reads the raw text the job's failure column holds.</summary>
    /// <param name="context">The data context to query through.</param>
    /// <param name="jobId">The job's id.</param>
    /// <returns>The column value.</returns>
    private static Task<string?> StoredErrorAsync(AppDbContext context, int jobId) =>
        context.Database
            .SqlQuery<string?>($"""SELECT "ErrorMessage" AS "Value" FROM "MigrationJobs" WHERE "Id" = {jobId}""")
            .SingleAsync();
}
