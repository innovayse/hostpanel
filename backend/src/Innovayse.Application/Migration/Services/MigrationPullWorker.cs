namespace Innovayse.Application.Migration.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pulls data page-by-page from the migration plugin and updates the job's progress.
/// Runs in a background task; uses a dedicated DI scope.
/// </summary>
public sealed class MigrationPullWorker(
    IMigrationJobRepository repo,
    IHttpClientFactory httpClientFactory,
    ILogger<MigrationPullWorker> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int PerPage = 50;

    /// <summary>Executes the full pull for a job identified by <paramref name="jobId"/>.</summary>
    public async Task RunAsync(int jobId, CancellationToken ct)
    {
        try
        {
            await PullAsync(jobId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration pull failed for job {JobId}", jobId);
            try
            {
                var job = await repo.GetByIdAsync(jobId, ct);
                if (job is not null)
                {
                    job.Fail(ex.Message);
                    await repo.SaveAsync(ct);
                }
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to save failure state for job {JobId}", jobId);
            }
        }
    }

    private async Task PullAsync(int jobId, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Migration job {jobId} not found.");

        var client = httpClientFactory.CreateClient("migration");

        // 1. Fetch totals
        var totalsResp = await PostActionAsync<TotalsResponse>(client, job.SourceUrl, job.Key, "totals", null, ct);
        job.Start(
            job.ExportClients  ? totalsResp.Clients  : 0,
            job.ExportInvoices ? totalsResp.Invoices : 0,
            job.ExportServices ? totalsResp.Services : 0,
            job.ExportDomains  ? totalsResp.Domains  : 0,
            job.ExportTickets  ? totalsResp.Tickets  : 0);
        await repo.SaveAsync(ct);

        // 2. Pull each entity type
        if (job.ExportClients)
            await PullEntityAsync(client, job, "clients", MigrationEntityType.Clients, ct);

        if (job.ExportInvoices)
            await PullEntityAsync(client, job, "invoices", MigrationEntityType.Invoices, ct);

        if (job.ExportServices)
            await PullEntityAsync(client, job, "services", MigrationEntityType.Services, ct);

        if (job.ExportDomains)
            await PullEntityAsync(client, job, "domains", MigrationEntityType.Domains, ct);

        if (job.ExportTickets)
            await PullEntityAsync(client, job, "tickets", MigrationEntityType.Tickets, ct);

        job.Complete();
        await repo.SaveAsync(ct);
        logger.LogInformation("Migration job {JobId} completed successfully.", jobId);
    }

    private async Task PullEntityAsync(
        HttpClient client,
        MigrationJob job,
        string action,
        MigrationEntityType entityType,
        CancellationToken ct)
    {
        int page = 1;
        int imported = 0;

        while (true)
        {
            var extra = new Dictionary<string, object> { ["page"] = page, ["perPage"] = PerPage };
            var resp = await PostActionAsync<PagedResponse>(client, job.SourceUrl, job.Key, action, extra, ct);

            imported += resp.Items?.Count ?? 0;
            job.UpdateProgress(entityType, imported);
            await repo.SaveAsync(ct);

            if (page >= resp.TotalPages || resp.TotalPages == 0)
                break;

            page++;
        }
    }

    private async Task<T> PostActionAsync<T>(
        HttpClient client,
        string sourceUrl,
        string key,
        string action,
        Dictionary<string, object>? extra,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object> { ["key"] = key, ["action"] = action };
        if (extra is not null)
            foreach (var kv in extra) payload[kv.Key] = kv.Value;

        var response = await client.PostAsJsonAsync(sourceUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Empty response for action '{action}'.");
    }

    // ── Response DTOs ────────────────────────────────────────────────────────

    private sealed class TotalsResponse
    {
        public int Clients  { get; set; }
        public int Invoices { get; set; }
        public int Services { get; set; }
        public int Domains  { get; set; }
        public int Tickets  { get; set; }
    }

    private sealed class PagedResponse
    {
        public List<JsonElement>? Items      { get; set; }
        public int                TotalPages { get; set; }
    }
}
