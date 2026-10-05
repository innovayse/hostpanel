namespace Innovayse.Application.Tests.Admin.Integrations;

using Innovayse.Application.Admin.Integrations;
using Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="TestIntegrationConnectionHandler"/>, focused on the
/// live-probe branches: the "inecobank" plugin and built-in probes such as "nameam".</summary>
public class TestIntegrationConnectionHandlerTests
{
    private readonly Mock<ISettingRepository> settings = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();

    private readonly Mock<IIntegrationConnectionProbe> nameAmProbe = new();

    /// <summary>Wires the "nameam" probe mock into every handler under test.</summary>
    public TestIntegrationConnectionHandlerTests()
    {
        nameAmProbe.SetupGet(p => p.Slug).Returns("nameam");
    }

    private TestIntegrationConnectionHandler CreateHandler() =>
        new(settings.Object, resolver.Object, [nameAmProbe.Object]);

    /// <summary>Stores both required "nameam" fields so execution reaches the probe.</summary>
    private void SeedNameAmSettings()
    {
        var stored = new List<Setting>
        {
            Setting.Create("integration:nameam:email", "user@example.com", null),
            Setting.Create("integration:nameam:password", "secret", null),
        };
        settings.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
    }

    [Fact]
    public async Task HandleAsync_NameAm_ProbeSucceeds_ReportsCredentialsAccepted()
    {
        SeedNameAmSettings();

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("nameam"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("credentials accepted", result.Message, StringComparison.OrdinalIgnoreCase);
        nameAmProbe.Verify(p => p.ProbeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NameAm_ProbeThrows_ReportsFailureWithMessage()
    {
        // A filled-in but wrong password used to read "Connection OK"; it must now fail.
        SeedNameAmSettings();
        nameAmProbe.Setup(p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)."));

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("nameam"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("401", result.Message);
    }

    [Fact]
    public async Task HandleAsync_NameAm_MissingPassword_DoesNotProbe()
    {
        settings.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [Setting.Create("integration:nameam:email", "user@example.com", null)]);

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("nameam"), CancellationToken.None);

        Assert.False(result.Success);
        nameAmProbe.Verify(p => p.ProbeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Stores all three required fields for "inecobank" so the handler's
    /// missing-fields check passes and execution reaches the live-probe branch.
    /// </summary>
    private void SeedConfiguredSettings()
    {
        var stored = new List<Setting>
        {
            Setting.Create("integration:inecobank:gateway_url", "https://gateway.example.com", null),
            Setting.Create("integration:inecobank:username", "merchant", null),
            Setting.Create("integration:inecobank:password", "secret", null),
        };
        settings.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ResolverReturnsNull_ReportsDisabledOrNotLoaded()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("inecobank"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        resolver.Verify(
            r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ProbeReturnsNormally_ReportsSuccess()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("inecobank"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("reachable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credentials accepted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ProbeThrows_ReportsFailureWithMessage()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Access denied (errorCode 5)."));

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("inecobank"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Access denied (errorCode 5).", result.Message);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ProbeCancelled_PropagatesRatherThanReportingFailure()
    {
        // An admin-cancelled probe (e.g. navigating away mid-test) must not be reported as
        // "Gateway test failed" — that message is reserved for actual gateway failures.
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("inecobank"), CancellationToken.None));
    }
}
