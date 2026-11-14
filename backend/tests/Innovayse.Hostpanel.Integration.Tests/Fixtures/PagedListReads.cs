using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Reads the ids and the total of one page of an admin list that answers the shared paged shape.</summary>
public static class PagedListReads
{
    /// <summary>Reads one page of a list.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    public static async Task<(List<int> Ids, int TotalCount)> ReadIdsAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (ReadIds(body.RootElement), body.RootElement.GetProperty("totalCount").GetInt32());
    }

    /// <summary>Reads the ids of the <c>items</c> of a paged answer.</summary>
    /// <param name="page">The paged answer.</param>
    /// <returns>The ids in the order the list gave them.</returns>
    public static List<int> ReadIds(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }
}
