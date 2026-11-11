using Innovayse.Providers.CWP7;
using Innovayse.Providers.CWP7.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Cwp7;

/// <summary>
/// Runs <see cref="Cwp7ApiClient.GetAutoLoginUrlAsync"/> against a real CWP7 server.
/// </summary>
/// <remarks>
/// Skipped, and reported as skipped, unless a server is configured; see
/// <see cref="LiveCwp7FactAttribute"/>. It exists because the unit tests prove the parsing against
/// a body captured by hand, and would go on passing if the route were wrong again: only a real
/// call can catch that. Run against a staging account, not a customer's.
/// </remarks>
public sealed class Cwp7LiveAutoLoginTests
{
    /// <summary>Auto login, issues ausable session url, against the real server.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [LiveCwp7Fact]
    public async Task AutoLogin_IssuesAUsableSessionUrl_AgainstTheRealServerAsync()
    {
        var host = Environment.GetEnvironmentVariable("CWP7_LIVE_HOST")!;
        var key = Environment.GetEnvironmentVariable("CWP7_LIVE_KEY")!;
        var user = Environment.GetEnvironmentVariable("CWP7_LIVE_USER")!;

        // The panel's certificate is not the point of this test and is often self-signed.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var client = new Cwp7ApiClient(http, NullLogger<Cwp7ApiClient>.Instance);

        var result = await client.GetAutoLoginUrlAsync(new Cwp7Connection(host, key, host), user, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Message);
        result.Message.Should().StartWith("https://").And.Contain("user_session=");
    }
}
