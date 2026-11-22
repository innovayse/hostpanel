using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The one comparison of two plugin refusals the plugin door tests share: a plugin the host does not serve must be refused
/// with the very body an id that was never loaded gets, so the answer tells a caller nothing about which plugins exist, and
/// a service of another client must be refused as a service that does not exist (#1357).
/// </summary>
public static class PluginRefusalBodies
{
    /// <summary>The members of the error body that never differ between two refusals of the same reason.</summary>
    private static readonly string[] StableMembers = ["type", "title", "status", "detail"];

    /// <summary>Asserts that two answers are the same 404 with the same code.</summary>
    /// <param name="refused">The answer for the plugin or service the host must not reveal.</param>
    /// <param name="unknown">The answer for a plugin id that was never loaded, or a service that does not exist.</param>
    /// <param name="code">The error code both bodies carry in <c>type</c>.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    public static async Task AssertSameAsync(HttpResponseMessage refused, HttpResponseMessage unknown, string code)
    {
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertIdenticalAsync(refused, unknown);
        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(code);
    }

    /// <summary>Asserts that two answers have the same status and the same error body, whatever that status and code are.</summary>
    /// <param name="refused">The answer for the plugin or service the host must not reveal.</param>
    /// <param name="unknown">The answer for a plugin id that was never loaded, or a service that does not exist.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    public static async Task AssertIdenticalAsync(HttpResponseMessage refused, HttpResponseMessage unknown)
    {
        refused.StatusCode.Should().Be(unknown.StatusCode);
        using var refusedBody = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        using var unknownBody = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());

        // The four ErrorResponse members only: a per-request member added later (a trace id) must not fail the comparison.
        foreach (var member in StableMembers)
        {
            refusedBody.RootElement.GetProperty(member).GetRawText().Should().Be(unknownBody.RootElement.GetProperty(member).GetRawText());
        }
    }
}
