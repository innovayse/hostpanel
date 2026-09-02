namespace Innovayse.Infrastructure.Tests.Integrations.Cwp7;

using FluentAssertions;
using Innovayse.Providers.CWP7;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Runs <see cref="Cwp7ApiClient.GetAutoLoginUrlAsync"/> against a real CWP7 server.
/// </summary>
/// <remarks>
/// Skipped, and reported as skipped, unless a server is configured — see
/// <see cref="LiveCwp7FactAttribute"/>. It exists because the unit tests prove the parsing against
/// a body captured by hand, and would go on passing if the route were wrong again: only a real
/// call can catch that. Run against a staging account, not a customer's.
/// </remarks>
public sealed class Cwp7LiveAutoLoginTests
{
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

        var result = await client.GetAutoLoginUrlAsync(host, key, user, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Message);
        result.Message.Should().StartWith("https://").And.Contain("user_session=");
    }
}
