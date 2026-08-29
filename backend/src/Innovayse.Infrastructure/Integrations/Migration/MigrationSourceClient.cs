namespace Innovayse.Infrastructure.Integrations.Migration;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Migration.Common;
using Innovayse.Application.Migration.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Talks to the legacy system's migration plugin over HTTP: every operation is a JSON POST to the
/// job's configured URL carrying the job key and the name of the operation, and the plugin answers
/// with a JSON body. This is the only place in the product that knows the migration source speaks
/// HTTP at all.
/// </summary>
/// <param name="httpClientFactory">Factory for the named client that carries the pull timeout.</param>
/// <param name="logger">Logger used to record what the source answered when it refused a request.</param>
public sealed class MigrationSourceClient(
    IHttpClientFactory httpClientFactory,
    ILogger<MigrationSourceClient> logger) : IMigrationSource
{
    /// <summary>
    /// Name of the configured <see cref="HttpClient"/>. It carries a deliberately long timeout,
    /// because a single page of a large export can take minutes to assemble on the source side.
    /// </summary>
    private const string HttpClientName = "migration";

    /// <summary>Operation name the source expects for a bare connectivity check.</summary>
    private const string PingAction = "ping";

    /// <summary>Operation name the source expects when asked for its per-kind record counts.</summary>
    private const string TotalsAction = "totals";

    /// <summary>
    /// How responses are read. Case-insensitive because the source names its fields in its own
    /// convention rather than ours, and a mismatch there would silently yield zeroes and nulls.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task PingAsync(string sourceUrl, string accessKey, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        var payload = new { key = accessKey, action = PingAction };
        var response = await client.PostAsJsonAsync(sourceUrl, payload, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public Task<MigrationSourceTotals> GetTotalsAsync(
        string sourceUrl,
        string accessKey,
        CancellationToken ct = default) =>
        PostActionAsync<MigrationSourceTotals>(sourceUrl, accessKey, TotalsAction, null, ct);

    /// <inheritdoc />
    public Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        string sourceUrl,
        string accessKey,
        string dataSet,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        var extra = new Dictionary<string, object> { ["page"] = page, ["perPage"] = perPage };
        return PostActionAsync<MigrationRecordPage<TRecord>>(sourceUrl, accessKey, dataSet, extra, ct);
    }

    /// <summary>
    /// Posts one operation to the source and reads its answer.
    /// </summary>
    /// <typeparam name="T">Shape the answer is read back as.</typeparam>
    /// <param name="sourceUrl">Where the source listens.</param>
    /// <param name="key">The job's shared key, which the source checks before answering.</param>
    /// <param name="action">The operation to run — the source's own name for it.</param>
    /// <param name="extra">
    /// Additional fields to send alongside the key and the action, or null where the operation
    /// takes none. Added after the two mandatory fields, never replacing them.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized answer.</returns>
    /// <exception cref="HttpRequestException">The source answered a non-success status.</exception>
    /// <exception cref="InvalidOperationException">The source answered a body that read as null.</exception>
    private async Task<T> PostActionAsync<T>(
        string sourceUrl,
        string key,
        string action,
        Dictionary<string, object>? extra,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        var payload = new Dictionary<string, object> { ["key"] = key, ["action"] = action };
        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                payload[kv.Key] = kv.Value;
            }
        }

        var response = await client.PostAsJsonAsync(sourceUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Log the body before throwing: EnsureSuccessStatusCode discards it, and the source
            // puts the reason it refused in there rather than in the status.
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("WHMCS api.php returned {Status} for action '{Action}': {Body}",
                (int)response.StatusCode, action, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Empty response for action '{action}'.");
    }
}
