namespace Innovayse.Application.Migration.Queries.TestMigrationConnection;

using System.Net.Http.Json;
using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Pings the migration plugin at SourceUrl to verify connectivity.</summary>
public sealed class TestMigrationConnectionHandler(
    IMigrationJobRepository repo,
    IHttpClientFactory httpClientFactory)
{
    /// <summary>Sends a ping to the plugin and records the result.</summary>
    public async Task<MigrationJobDto> HandleAsync(TestMigrationConnectionQuery query, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException("Migration job not found.");

        var client = httpClientFactory.CreateClient("migration");

        var payload = new { key = job.Key, action = "ping" };
        var response = await client.PostAsJsonAsync(job.SourceUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        job.RecordPing();
        await repo.SaveAsync(ct);
        return job.ToDto();
    }
}
