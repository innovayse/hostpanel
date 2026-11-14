using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The readers every paged list test shares: one page of an admin list, the ids of its rows and its total, the fields a
/// refusal names, and a text unique to one test.
/// </summary>
public static class PagedListReads
{
    /// <summary>Reads a page of a list that must have been answered with 200.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The answer body.</returns>
    public static async Task<JsonElement> PageAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    /// <summary>Reads the ids of the <c>items</c> of a paged answer, in the order the page holds them.</summary>
    /// <param name="page">The paged answer.</param>
    /// <returns>The row ids.</returns>
    public static List<int> IdsOf(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }

    /// <summary>Reads the ids and the total of one page of a list.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    public static async Task<(List<int> Ids, int TotalCount)> ReadIdsAsync(HttpClient admin, string url)
    {
        var page = await PageAsync(admin, url);
        return (IdsOf(page), page.GetProperty("totalCount").GetInt32());
    }

    /// <summary>Reads the names of the fields a 400 refusal reports, in lower case.</summary>
    /// <param name="response">The refused response.</param>
    /// <returns>The field names the refusal carries.</returns>
    public static async Task<List<string>> RefusedFieldsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
    }

    /// <summary>
    /// Builds a text that is unique to one test and holds no digit, so a search for it can never match an id and a
    /// search for an id can never match it.
    /// </summary>
    /// <returns>Thirty-two lower-case letters.</returns>
    public static string UniqueToken() =>
        string.Concat(Guid.NewGuid().ToByteArray().SelectMany(b => new[] { (char)('a' + (b >> 4)), (char)('a' + (b & 15)) }));
}
