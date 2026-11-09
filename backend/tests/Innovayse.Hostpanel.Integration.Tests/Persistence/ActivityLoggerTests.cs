using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins <see cref="IActivityLogger"/>: a line joins the caller's unit of work, so it is written by
/// the caller's save and never on its own.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ActivityLoggerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A recorded line is saved by the caller's save, with the client and description it was given.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RecordAsync_ThenSave_WritesTheLineAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var description = $"Audited {Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IActivityLogger>()
                .RecordAsync(new ActivityLogEntry(seeded.ClientId, description), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);
        }

        using var check = factory.Services.CreateScope();
        var line = await check.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
            .SingleAsync(l => l.Description == description);
        line.ClientId.Should().Be(seeded.ClientId);
    }

    /// <summary>A recorded line whose caller never saves is not written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RecordAsync_WithoutSave_WritesNothingAsync()
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var description = $"Unsaved {Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IActivityLogger>()
                .RecordAsync(new ActivityLogEntry(seeded.ClientId, description), CancellationToken.None);
        }

        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
            .AnyAsync(l => l.Description == description)).Should().BeFalse();
    }
}
