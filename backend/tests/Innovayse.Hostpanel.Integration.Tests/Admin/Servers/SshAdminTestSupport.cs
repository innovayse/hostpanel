using System.Net.Http.Headers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>Shared setup for the server SSH settings tests.</summary>
internal static class SshAdminTestSupport
{
    /// <summary>A valid-looking pinned host key fingerprint.</summary>
    public const string Fingerprint = "SHA256:p6R0QgF93KJCG8T5VLbbvfWcFGdwzv2ViKOejXV5TLw";

    /// <summary>An HTTP client signed in as the admin.</summary>
    /// <param name="factory">The shared host.</param>
    /// <returns>The client.</returns>
    public static async Task<HttpClient> AdminAsync(IntegrationTestFactory factory)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return http;
    }

    /// <summary>A complete settings body.</summary>
    /// <param name="enabled">Whether to enable.</param>
    /// <param name="privateKey">The private key text, or null to keep the stored one.</param>
    /// <param name="host">Agent host; a fresh unique one by default so tests never share an endpoint.</param>
    /// <returns>The body.</returns>
    public static object Body(bool enabled, string? privateKey = "TEST-PRIVATE-KEY-MATERIAL", string? host = null) => new
    {
        isEnabled = enabled,
        sshHostname = "linux1.example.test",
        sshPort = 2222,
        agentHost = host ?? $"agent-{Guid.NewGuid():N}.internal.test",
        agentPort = 22,
        agentUsername = "hpsshkeys",
        agentPrivateKey = privateKey,
        agentHostKeyFingerprint = Fingerprint,
    };
}
