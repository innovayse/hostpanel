namespace Innovayse.Application.Tests.Admin.Integrations;

using Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="TestIntegrationConnectionHandler"/>, focused on the
/// live-probe branch added for the "innovayse-inecobank" slug.</summary>
public class TestIntegrationConnectionHandlerTests
{
    private readonly Mock<ISettingRepository> settings = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();

    private TestIntegrationConnectionHandler CreateHandler() => new(settings.Object, resolver.Object);

    /// <summary>
    /// Stores all three required fields for "innovayse-inecobank" so the handler's
    /// missing-fields check passes and execution reaches the live-probe branch.
    /// </summary>
    private void SeedConfiguredSettings()
    {
        var stored = new List<Setting>
        {
            Setting.Create("integration:innovayse-inecobank:gateway_url", "https://gateway.example.com", null),
            Setting.Create("integration:innovayse-inecobank:username", "merchant", null),
            Setting.Create("integration:innovayse-inecobank:password", "secret", null),
        };
        settings.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ResolverReturnsNull_ReportsDisabledOrNotLoaded()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("innovayse-inecobank"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        resolver.Verify(
            r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ProbeReturnsNormally_ReportsSuccess()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("innovayse-inecobank"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("reachable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credentials accepted", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_Inecobank_ProbeThrows_ReportsFailureWithMessage()
    {
        SeedConfiguredSettings();
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Access denied (errorCode 5)."));

        var result = await CreateHandler().HandleAsync(
            new TestIntegrationConnectionCommand("innovayse-inecobank"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Access denied (errorCode 5).", result.Message);
    }
}
