using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ConfigureStatusComponentMonitoring</c> use case:
/// <c>PUT /api/admin/status/components/{id}/monitoring</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConfigureStatusComponentMonitoringTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The address, the switch and the threshold are stored on the component.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Configure_StoresTheSettings()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/monitoring",
            new { checkUrl = "https://hosting.example.test/health", enabled = true, failureThreshold = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var component = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .StatusComponents.AsNoTracking().FirstAsync(c => c.Id == componentId, CancellationToken.None);
        component.CheckUrl.Should().Be("https://hosting.example.test/health");
        component.MonitoringEnabled.Should().BeTrue();
        component.FailureThreshold.Should().Be(4);
    }

    /// <summary>Switching monitoring on without an address is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Configure_EnabledWithoutAddress_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/monitoring",
            new { checkUrl = string.Empty, enabled = true, failureThreshold = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An address that is not a web address is refused, so the job never requests it.</summary>
    /// <param name="url">An address the validator refuses.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/health")]
    [InlineData("ftp://hosting.example.test/health")]
    public async Task Configure_NotAWebAddress_RefusesWithValidationError(string url)
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/monitoring",
            new { checkUrl = url, enabled = false, failureThreshold = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A threshold outside 1 to 10 is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <param name="threshold">A threshold the validator refuses.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Configure_ThresholdOutOfRange_RefusesWithValidationError(int threshold)
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/monitoring",
            new { checkUrl = "https://hosting.example.test/health", enabled = true, failureThreshold = threshold });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An id that names no component answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Configure_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/2147483000/monitoring",
            new { checkUrl = "https://hosting.example.test/health", enabled = true, failureThreshold = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Configure_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components/1/monitoring",
            new { checkUrl = "https://hosting.example.test/health", enabled = true, failureThreshold = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
