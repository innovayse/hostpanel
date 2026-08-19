namespace Innovayse.Infrastructure.Auth;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The SSO's service API, as this product uses it.
///
/// <para>
/// Authenticated with the shared service key rather than a user's token: these calls
/// happen on behalf of the product, not of whoever is signed in. The equivalent admin
/// endpoints authenticate a superadmin through a cookie or bearer token and so cannot be
/// called by a machine at all.
/// </para>
///
/// <para>
/// A missing account comes back as null. Anything else — the SSO refusing the key, being
/// unreachable, or failing — throws. That distinction is the whole point of this class:
/// a lookup that could not happen must never be reported as "no such person", because
/// the caller would act on it as a deleted account.
/// </para>
/// </summary>
public sealed class SsoServiceClient(HttpClient http)
{
    /// <summary>The account with this subject, or null if the SSO has no such account.</summary>
    public async Task<SsoAccount?> GetByIdAsync(string subject, CancellationToken ct)
    {
        using var response = await http.GetAsync($"api/service/users/{subject}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SsoUserResponse>(ct);
        return body is null ? null : new SsoAccount(subject, body.Email, body.FirstName, body.LastName, body.TwoFactorEnabled);
    }

    /// <summary>The account with this email address, or null.</summary>
    public async Task<SsoAccount?> GetByEmailAsync(string email, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"api/service/users/lookup?email={Uri.EscapeDataString(email)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var found = await response.Content.ReadFromJsonAsync<SsoLookupResponse>(ct);
        if (found is null || string.IsNullOrEmpty(found.UserId)) return null;

        // The lookup answers with an id alone, so the name and address come from a second
        // call. Two round trips, and worth it: the alternative is a lookup endpoint that
        // returns a whole account to every caller that only wanted to know it exists.
        return await GetByIdAsync(found.UserId, ct);
    }

    /// <summary>Accounts for many subjects at once, keyed by subject.</summary>
    public async Task<IReadOnlyDictionary<string, SsoAccount>> GetBatchAsync(
        IReadOnlyCollection<string> subjects, CancellationToken ct)
    {
        if (subjects.Count == 0) return new Dictionary<string, SsoAccount>();

        using var response = await http.PostAsJsonAsync(
            "api/service/users/batch", new { ids = subjects }, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SsoBatchResponse>(ct);
        if (body is null) return new Dictionary<string, SsoAccount>();

        return body.Users.ToDictionary(
            u => u.Id,
            u => new SsoAccount(u.Id, u.Email, u.FirstName, u.LastName, u.TwoFactorEnabled));
    }

    /// <summary>One page of accounts, with the unpaged total.</summary>
    public async Task<(IReadOnlyList<SsoAccount> Items, int Total)> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = $"api/service/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }

        using var response = await http.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SsoListResponse>(ct);
        if (body is null) return ([], 0);

        return (body.Users.Select(u => new SsoAccount(u.Id, u.Email, u.FirstName, u.LastName, u.TwoFactorEnabled)).ToList(),
                body.Total);
    }

    /// <summary>An account as the SSO describes it.</summary>
    public sealed record SsoAccount(
        string Subject, string Email, string FirstName, string LastName, bool TwoFactorEnabled);

    private sealed record SsoUserResponse(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("lastName")] string LastName,
        [property: JsonPropertyName("twoFactorEnabled")] bool TwoFactorEnabled);

    private sealed record SsoLookupResponse(
        [property: JsonPropertyName("userId")] string UserId);

    private sealed record SsoBatchResponse(
        [property: JsonPropertyName("users")] List<SsoListItem> Users);

    private sealed record SsoListResponse(
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("users")] List<SsoListItem> Users);

    private sealed record SsoListItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("lastName")] string LastName,
        [property: JsonPropertyName("twoFactorEnabled")] bool TwoFactorEnabled);
}
