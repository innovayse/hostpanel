using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Plesk;

/// <summary>
/// Runs a whole account lifecycle against a real Plesk server, skipped unless
/// <see cref="LivePleskFactAttribute"/> finds one configured.
/// </summary>
/// <remarks>
/// The stub tests prove the provider against reply shapes captured by hand; only a real server can
/// show the packets are ones Plesk accepts. Run it against a throw-away server such as the
/// <c>plesk/plesk</c> image, never one with customers on it.
/// </remarks>
public sealed class PleskLiveLifecycleTests
{
    /// <summary>Provision, read, suspend, unsuspend, change password and plan, terminate, terminate again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [LivePleskFact]
    public async Task Lifecycle_RunsEndToEnd_AgainstTheRealServerAsync()
    {
        var url = Environment.GetEnvironmentVariable("PLESK_LIVE_URL")!;
        var key = Environment.GetEnvironmentVariable("PLESK_LIVE_KEY")!;

        // The throw-away server's certificate is self-signed; it is not what this test is about.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        var connection = new PleskConnection(url, key);
        var client = new PleskApiClient(http, NullLogger<PleskApiClient>.Instance);
        var provider = new PleskProvisioningProvider(connection, null, client, NullLogger<PleskProvisioningProvider>.Instance);

        var user = $"hp{Random.Shared.Next(100000, 999999)}";
        var order = new ProvisionAccountOrder(1, $"{user}.test", user, "Xy7#pq9Lm2!a", "Default Domain");
        try
        {
            (await provider.ProvisionAsync(order, CancellationToken.None)).Outcome.Should().Be(ProvisioningOutcome.Created);

            var retry = await provider.ProvisionAsync(order, CancellationToken.None);
            retry.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
            retry.ExistingAccountDomain.Should().Be(order.DomainName);

            (await provider.GetCredentialsAsync(user, CancellationToken.None)).Domain.Should().Be(order.DomainName);

            await provider.SuspendAsync(new SuspendAccountOrder(1, user, "test"), CancellationToken.None);
            (await client.FindCustomerAsync(connection, user, CancellationToken.None)).Value!.IsSuspended.Should().BeTrue();
            await provider.SuspendAsync(new SuspendAccountOrder(1, user, "test"), CancellationToken.None);

            await provider.UnsuspendAsync(user, CancellationToken.None);
            (await client.FindCustomerAsync(connection, user, CancellationToken.None)).Value!.IsSuspended.Should().BeFalse();

            await provider.ChangePasswordAsync(user, "Nw9$ab3Kq7!z", CancellationToken.None);
            await provider.ChangePackageAsync(user, "Unlimited", CancellationToken.None);

            // The session is bound to the address the request reaches Plesk from, so the test asks
            // for it under PLESK_LIVE_CALLER_IP and then opens the link itself, from that address.
            var callerIp = Environment.GetEnvironmentVariable("PLESK_LIVE_CALLER_IP");
            if (!string.IsNullOrWhiteSpace(callerIp))
            {
                var link = await provider.GetCPanelSsoUrlAsync(user, callerIp, CancellationToken.None);

                using var browserHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    UseCookies = true,
                };
                using var browser = new HttpClient(browserHandler) { Timeout = TimeSpan.FromMinutes(1) };
                using var landing = await browser.GetAsync(link, CancellationToken.None);

                // An accepted session lands inside the customer's panel; a refused one on the sign-in page.
                landing.RequestMessage!.RequestUri!.AbsolutePath.Should().NotStartWith("/login_up.php");
            }

            await provider.TerminateAsync(new TerminateAccountOrder(1, user, "test"), CancellationToken.None);
            await provider.TerminateAsync(new TerminateAccountOrder(1, user, "test"), CancellationToken.None);
            (await client.FindCustomerAsync(connection, user, CancellationToken.None)).Outcome
                .Should().Be(Infrastructure.Integrations.Plesk.Models.Enums.PleskLookupOutcome.NotFound);
        }
        finally
        {
            await client.DeleteCustomerAsync(connection, user, CancellationToken.None);
        }
    }
}
