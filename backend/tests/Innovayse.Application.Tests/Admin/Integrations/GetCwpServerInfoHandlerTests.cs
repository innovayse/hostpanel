namespace Innovayse.Application.Tests.Admin.Integrations;

using System.Globalization;
using Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

/// <summary>Unit tests for <see cref="GetCwpServerInfoHandler"/>.</summary>
public sealed class GetCwpServerInfoHandlerTests
{
    /// <summary>Creates a real <see cref="IMemoryCache"/> backed by the default in-memory store.</summary>
    /// <returns>A new empty <see cref="IMemoryCache"/> instance.</returns>
    private static IMemoryCache CreateCache() =>
        new MemoryCache(new MemoryCacheOptions());

    /// <summary>
    /// When the CWP host setting is missing, the handler returns a not-connected DTO
    /// without calling the API client.
    /// </summary>
    [Fact]
    public async Task Handle_WhenHostMissing_ReturnsNotConnectedAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);

        var cwpClient = new Mock<ICwpApiClient>();
        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, CreateCache());

        // Act
        var result = await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert
        Assert.False(result.Connected);
        Assert.Equal("CWP is not configured.", result.ErrorMessage);
        Assert.Null(result.AccountsCount);
        Assert.Null(result.CwpVersion);
        Assert.Null(result.LastTestedAt);
        cwpClient.VerifyNoOtherCalls();
    }

    /// <summary>
    /// When the CWP API key setting is missing, the handler returns a not-connected DTO
    /// without calling the API client.
    /// </summary>
    [Fact]
    public async Task Handle_WhenApiKeyMissing_ReturnsNotConnectedAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:host", "cwp.example.com", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:api_key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);

        var cwpClient = new Mock<ICwpApiClient>();
        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, CreateCache());

        // Act
        var result = await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert
        Assert.False(result.Connected);
        Assert.Equal("CWP is not configured.", result.ErrorMessage);
        cwpClient.VerifyNoOtherCalls();
    }

    /// <summary>
    /// When credentials are valid and the CWP API succeeds,
    /// the handler returns a connected DTO populated with real data.
    /// </summary>
    [Fact]
    public async Task Handle_WhenApiSucceeds_ReturnsConnectedWithDataAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:host", "cwp.example.com", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:port", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:port", "2031", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:api_key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:api_key", "secret-api-key", null));

        var cwpClient = new Mock<ICwpApiClient>();
        cwpClient
            .Setup(c => c.GetServerInfoAsync("cwp.example.com", "2031", "secret-api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((42, "1.9.1"));

        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, CreateCache());

        // Act
        var result = await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.Connected);
        Assert.Equal(42, result.AccountsCount);
        Assert.Equal("1.9.1", result.CwpVersion);
        Assert.NotNull(result.LastTestedAt);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// When the CWP API throws an exception, the handler returns a not-connected DTO
    /// containing the exception message.
    /// </summary>
    [Fact]
    public async Task Handle_WhenApiThrows_ReturnsNotConnectedWithErrorMessageAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:host", "bad-host", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:port", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:api_key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:api_key", "key", null));

        var cwpClient = new Mock<ICwpApiClient>();
        cwpClient
            .Setup(c => c.GetServerInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, CreateCache());

        // Act
        var result = await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert
        Assert.False(result.Connected);
        Assert.Equal("Connection refused", result.ErrorMessage);
    }

    /// <summary>
    /// After a successful call, subsequent calls within 5 minutes must return
    /// the cached result without hitting the repository or API client again.
    /// </summary>
    [Fact]
    public async Task Handle_SecondCall_ReturnsCachedResultAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:host", "cwp.example.com", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:port", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:api_key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:api_key", "key", null));

        var cwpClient = new Mock<ICwpApiClient>();
        cwpClient
            .Setup(c => c.GetServerInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((10, "1.9.0"));

        var cache = CreateCache();
        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, cache);

        // Act
        await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);
        await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert — API called exactly once despite two handler invocations
        cwpClient.Verify(
            c => c.GetServerInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// With no port configured, the handler asks the CWP <b>API</b> port and not the panel's
    /// HTTPS user interface.
    /// </summary>
    /// <remarks>
    /// The fallback here was 2031, which is the CWP web UI. It answers nothing under
    /// <c>/v1/</c>, the only path this client requests, so an operator who had filled in a host
    /// and an API key but left the port blank was told the server was unreachable while it was
    /// answering perfectly on 2304 — the port every other CWP caller in this repository already
    /// used. The expectation is written against
    /// <see cref="ICwpApiClient.DefaultApiPort"/> rather than a literal so that the fallback and
    /// its test cannot drift apart, which is how the two got out of step in the first place.
    /// </remarks>
    [Fact]
    public async Task Handle_WhenPortNotConfigured_UsesTheApiPortAsync()
    {
        // Arrange
        var settingsRepo = new Mock<ISettingRepository>();
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:host", "cwp.example.com", null));
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:port", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);
        settingsRepo
            .Setup(r => r.FindByKeyAsync("integration:innovayse-cwp:api_key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create("integration:innovayse-cwp:api_key", "secret-api-key", null));

        var expectedPort = ICwpApiClient.DefaultApiPort.ToString(CultureInfo.InvariantCulture);

        var cwpClient = new Mock<ICwpApiClient>();
        cwpClient
            .Setup(c => c.GetServerInfoAsync("cwp.example.com", expectedPort, "secret-api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((7, "1.9.2"));

        var handler = new GetCwpServerInfoHandler(settingsRepo.Object, cwpClient.Object, CreateCache());

        // Act
        var result = await handler.HandleAsync(new GetCwpServerInfoQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.Connected);
        Assert.Equal(7, result.AccountsCount);
        Assert.NotEqual("2031", expectedPort);
        cwpClient.Verify(
            c => c.GetServerInfoAsync("cwp.example.com", expectedPort, "secret-api-key", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
